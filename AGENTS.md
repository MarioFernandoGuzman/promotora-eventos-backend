# AGENTS.md — Promotora de Eventos (Backend)

> Fuente de verdad para cualquier agente de IA (ChatGPT/Codex, Claude Code, Cursor, Copilot...).
> Léelo completo antes de tocar código. Si algo contradice a otro documento, **este archivo manda**.
>
> - Modelo de datos completo: [`docs/BASE_DE_DATOS.md`](docs/BASE_DE_DATOS.md)
> - Tareas y estado de avance: [`docs/PLAN_IMPLEMENTACION.md`](docs/PLAN_IMPLEMENTACION.md)

---

## 1. Proyecto

Plataforma para una **promotora de eventos** (referencia: ASA Promotions). Gestiona conciertos, partidos, vigilias, cultos, carreras (5k/10k/21k/42k) y meet & greets.

Capacidades: venta de boletos por localidad o entrada general, **venta de alta demanda con colas asíncronas**, gestión financiera (cuentas y movimientos), creación de eventos, reseñas de eventos/presentadores, mapa de recintos para elegir localidad, agente de IA con RAG, reportería (15 reportes, Excel/PDF) y BI (Power BI).

Perfiles: **CLIENTE**, **ADMINISTRADOR**, **DUENO_RECINTO**.

### Restricciones del proyecto (no negociables)
- Backend **prohibido** en Node.js+JavaScript y en FastAPI/Python. Este proyecto es **.NET / C#**.
- BD real (**PostgreSQL**). Nunca SQLite.
- Arquitectura formal por capas (**Clean Architecture**). Nunca un solo archivo para todo.
- RabbitMQ en al menos un flujo real de negocio (la compra de boletos).
- IA: agente que responde información **pública** + RAG con ingesta, recuperación y **citación**.
- Tablas de auditoría y de logs del agente de IA.
- ≥ 15 reportes exportables a Excel y PDF + integración con Power BI.
- Git en GitHub con ramas, Pull Requests e historial legible.

---

## 2. Stack

| Necesidad | Tecnología |
|---|---|
| Lenguaje / runtime | C# 13+, **.NET 10 (LTS)** — si el equipo solo tiene .NET 8, cambiar `TargetFramework` a `net8.0`; nada más cambia |
| API REST | ASP.NET Core Web API (controllers), Swagger/OpenAPI (`Swashbuckle` o `Microsoft.AspNetCore.OpenApi`) |
| Base de datos | PostgreSQL 16 + extensión **pgvector** (imagen `pgvector/pgvector:pg16`) |
| ORM | EF Core + Npgsql (+ `EFCore.NamingConventions` para snake_case) |
| Migraciones | **SQL versionado con Flyway** (`database/migrations`). EF Core **NO** genera migraciones |
| Mensajería | **RabbitMQ** con `RabbitMQ.Client` (API async v7) detrás de puertos propios |
| LLM y embeddings | **OpenAI (ChatGPT)** con el SDK oficial `OpenAI` (NuGet) detrás de `ILlmPort` / `IEmbeddingPort` |
| Vector search | `Pgvector.EntityFrameworkCore` (índice HNSW, coseno) |
| Extracción de texto | `UglyToad.PdfPig` (PDF), `DocumentFormat.OpenXml` (DOCX), texto/MD directo |
| Validación | FluentValidation |
| Seguridad | JWT Bearer + RBAC con policies; hash de contraseñas con Argon2/BCrypt/PBKDF2 (nunca MD5/SHA simple) |
| Excel / PDF | `ClosedXML` / `QuestPDF` (revisar licencia Community de QuestPDF) |
| Logging | Serilog (estructurado, con CorrelationId) |
| Pruebas | xUnit, Testcontainers (Postgres+pgvector, RabbitMQ), NetArchTest, Shouldly o asserts de xUnit |
| Contenedores | Docker Compose (postgres, rabbitmq, mailhog, flyway) |

**Evitar** librerías que hoy tienen licencia comercial o dudosa para un proyecto académico: MediatR, AutoMapper, MassTransit (v9+), FluentAssertions (v8+). Usamos handlers propios y mapeo manual. Verifica la licencia de cualquier paquete nuevo antes de agregarlo.

---

## 3. Arquitectura: Clean Architecture

```
Promotora.sln
src/
  Promotora.Domain/           Entidades, value objects, enums, eventos de dominio, reglas puras.
                              NO referencia a nada (ni EF, ni NuGets de infraestructura).
  Promotora.Application/      Casos de uso (commands/queries + handlers), DTOs, validadores y
                              PUERTOS (interfaces). Referencia solo a Domain.
  Promotora.Infrastructure/   ADAPTADORES: EF Core, RabbitMQ, OpenAI, SMTP, JWT, exportadores,
                              pasarela de pago. Implementa los puertos de Application.
  Promotora.WebApi/           Controllers, middlewares, auth, DI. Composition root del API.
  Promotora.Worker/           Host de procesos de fondo: consumidores RabbitMQ, outbox relay,
                              job de expiración de compras. Composition root del worker.
tests/
  Promotora.Domain.Tests/  Promotora.Application.Tests/  Promotora.Infrastructure.Tests/
  Promotora.Api.Tests/     Promotora.Architecture.Tests/
database/
  migrations/   V1__*.sql ...      (Flyway)
  seed-dev/     datos de prueba (solo desarrollo)
docs/
docker-compose.yml
```

### Regla de dependencia (se verifica con NetArchTest en CI)
```
WebApi ─┐
Worker ─┼──► Infrastructure ──► Application ──► Domain
        └──────────────────────► Application
```
- `Domain` no depende de nadie. `Application` solo de `Domain`. `Infrastructure` de `Application` (y `Domain`).
- `WebApi` y `Worker` son los únicos que conocen `Infrastructure` (para registrar DI).
- **Los controllers nunca usan `DbContext`**, ni RabbitMQ, ni OpenAI directamente: llaman a handlers de `Application`.
- `Application` define los puertos; `Infrastructure` los implementa. Ej.: `IUnitOfWork`, `ICompraRepository`, `IMessagePublisher`, `ILlmPort`, `IEmbeddingPort`, `IVectorSearchPort`, `IPaymentGateway`, `IEmailSender`, `IReportExporter`, `IAuditLogger`, `IClock`, `ITicketTokenService`.

### Organización por módulos (carpetas de feature dentro de cada capa)
`Identity` · `Venues` (recintos, localidades) · `Catalog` (eventos, categorías, presentadores, tipos de entrada) · `Ticketing` (compras, boletos) · `Finance` (pagos, cuentas, movimientos) · `Reviews` · `AI` (agente + RAG) · `Reporting` · `Audit` · `Shared`.
Un caso de uso de un módulo **no invoca handlers de otro módulo**: usa puertos o eventos de dominio.

### Patrón de casos de uso
```csharp
public sealed record CrearCompraCommand(int EventoId, IReadOnlyList<ItemCompra> Items, string IdempotencyKey) : ICommand<CompraCreadaDto>;

public sealed class CrearCompraHandler : ICommandHandler<CrearCompraCommand, CompraCreadaDto> { ... }
```
- Un handler = un caso de uso. Devuelve `Result<T>` para errores esperados (agotado, no autorizado, validación); excepciones solo para lo excepcional.
- Validación con FluentValidation antes del handler (decorador/pipeline propio).
- Errores HTTP con `ProblemDetails` (RFC 7807). Códigos: 400 validación, 401/403 auth, 404, 409 conflicto/agotado, 422 regla de negocio, 429 rate limit.
- API versionada: `/api/v1/...`. JSON en camelCase. Paginación `?page=&pageSize=` en todos los listados.

### Idioma y nombres
- **Dominio en español, igual que la BD** (`Evento`, `Compra`, `Boleto`, `TipoEntrada`, `Localidad`); sufijos técnicos en inglés (`Handler`, `Repository`, `Command`, `Query`, `Dto`).
- Tablas/columnas en `snake_case` (mapeo automático con `UseSnakeCaseNamingConvention`).
- C#: `Nullable` habilitado, `ImplicitUsings`, namespaces file-scoped, `async` de punta a punta, **`CancellationToken` en toda operación async**, `sealed` por defecto, DTOs como `record`.
- Mapeo entidad↔DTO **manual** (métodos de extensión o clases `Mapper`), sin AutoMapper.

---

## 4. Comandos

> Existirán al terminar la **Fase 2** del plan. Si un comando no existe aún, no lo inventes: revisa el plan.

```bash
# Infraestructura local (postgres+pgvector, rabbitmq, mailhog, flyway)
docker compose up -d
docker compose run --rm flyway migrate        # aplica database/migrations
docker compose run --rm flyway info           # estado de migraciones

# .NET
dotnet restore
dotnet build -warnaserror
dotnet test                                    # requiere Docker (Testcontainers)
dotnet format --verify-no-changes
dotnet run --project src/Promotora.WebApi      # API + Swagger en /swagger
dotnet run --project src/Promotora.Worker      # consumidores + outbox relay + expiración

# Secretos (nunca en git)
dotnet user-secrets set "OpenAI:ApiKey" "<tu-key>" --project src/Promotora.WebApi
dotnet user-secrets set "OpenAI:ApiKey" "<tu-key>" --project src/Promotora.Worker
```

Puertos locales de referencia: Postgres 5432 · RabbitMQ 5672 (UI 15672) · MailHog SMTP 1025 (UI 8025).

---

## 5. Reglas de negocio críticas (inviolables)

1. **Nunca sobrevender.** La reserva de cupo es un `UPDATE` atómico y se verifica por filas afectadas:
   ```csharp
   var filas = await db.TiposEntrada
       .Where(t => t.Id == id && t.CupoDisponible >= n)
       .ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, t => t.CupoDisponible - n), ct);
   if (filas == 0) return Result.Fail(Errores.Agotado);
   ```
   Prohibido el patrón leer-cupo → comparar en C# → guardar.
2. **Nunca borrar datos financieros ni de compras.** Sin `DELETE` sobre `compras`, `detalle_compra`, `boletos`, `pagos`, `movimientos_financieros`, `logs_*`. Se cambia el estado (`CANCELADA`, `ANULADO`...).
3. **El ledger es inmutable.** `movimientos_financieros` solo admite `INSERT`. Una corrección es un movimiento contrario. `cuentas_financieras.saldo` se modifica en la misma transacción que el movimiento y debe igualar `SUM(monto)`.
4. **Idempotencia.** `POST /api/v1/compras` exige header `Idempotency-Key` (se guarda en `compras.idempotency_key UNIQUE`; si se repite, se devuelve la compra existente). Los consumidores registran `mensajes_procesados(mensaje_id, consumidor)` en la misma transacción; si ya existe, hacen ACK y omiten.
5. **Una compra pertenece a un solo evento** (`compras.evento_id`); todos sus `tipos_entrada` deben ser de ese evento. Máximo `eventos.max_boletos_por_compra` boletos.
6. **Reserva con expiración.** Compra `PENDIENTE` reserva cupo y tiene `expira_en`. Si expira, falla o se cancela, el cupo se devuelve **una sola vez** (operación idempotente).
7. **Outbox.** Nunca publicar a RabbitMQ dentro de la transacción de negocio. Se escribe en `outbox_mensajes` en la misma transacción; el relay publica después.
8. **QR firmado.** `boletos.codigo_qr` es un token HMAC-SHA256 (no adivinable). Validar entrada = `UPDATE boletos SET estado='USADO', fecha_uso=now(), validado_por=@u WHERE codigo_qr=@c AND estado='VALIDO'` (0 filas ⇒ rechazado).
9. **Reseñas:** solo un cliente con boleto `USADO` de ese evento, una por (evento, cliente), nacen `PENDIENTE` y las modera un ADMINISTRADOR.
10. **Autorización por propiedad.** Un DUENO_RECINTO solo gestiona sus recintos/eventos/cuentas; un CLIENTE solo ve sus compras y boletos. Verificar propiedad en el handler, no solo el rol en el controller.
11. **Todo `TIMESTAMPTZ`; el código usa UTC** (`IClock`, nunca `DateTime.Now`).
12. **El agente de IA nunca accede a compras, usuarios ni finanzas.** Solo datos públicos y documentos RAG indexados.

---

## 6. Mensajería (RabbitMQ)

Exchange `promotora.compras` (direct, durable). Mensajes persistentes, *publisher confirms*, ACK manual, `prefetch` configurable.

| Routing key | Cola | Consumidor | Acción |
|---|---|---|---|
| `compra.procesar` | `compras.procesar` | `ProcesarCompraConsumer` | Cobra (`IPaymentGateway`), emite boletos, registra pago y movimientos, marca `COMPLETADA`/`FALLIDA` |
| `compra.completada` | `notificaciones.compra` | `EnviarConfirmacionConsumer` | Envía correo con boletos y guarda en `notificaciones` |
| `usuario.registrado` | `notificaciones.registro` | `EnviarBienvenidaConsumer` | Correo de bienvenida |

Reintentos: cola `compras.procesar.retry` (TTL con backoff, DLX de vuelta al exchange); tras N intentos (header `x-intentos`, por defecto 3) → `compras.procesar.dlq` vía exchange `promotora.dlx`.

Flujo de compra:
1. `POST /api/v1/compras` valida → reserva cupo atómico → crea compra `PENDIENTE` (`expira_en`) → inserta en `outbox_mensajes` (misma tx) → **202 Accepted** + `Location`.
2. `Worker` (outbox relay, `FOR UPDATE SKIP LOCKED`) publica a `compra.procesar`.
3. Consumidor procesa, completa y emite `compra.completada`.
4. Job de expiración marca `EXPIRADA` las `PENDIENTE` vencidas y devuelve cupo.
5. El cliente consulta `GET /api/v1/compras/{publicId}` (polling) hasta ver el estado final.

La pasarela de pago es un **puerto**; el adaptador del proyecto es simulado (`FakePaymentGateway`, tasa de fallo configurable para pruebas).

---

## 7. IA: agente público + RAG

- **Ingesta** (`POST /api/v1/ia/documentos`, solo ADMINISTRADOR): subir archivo → extraer texto → *chunking* (~800 tokens, solapamiento ~100) → embeddings en lote → `documentos_rag` + `chunks_rag`. Estados del documento: `PENDIENTE → INDEXADO | ERROR`.
- **Recuperación:** embedding de la pregunta → top-k por similitud coseno en `chunks_rag` (solo documentos `INDEXADO`) → umbral mínimo de similitud.
- **Generación con citación:** el prompt incluye los chunks con ID; la respuesta devuelve las fuentes (`documento`, `página`, `chunkId`) que se guardan en `logs_agente_ia.fuentes_citadas`. **Si no hay contexto suficiente, responde que no lo sabe**; no inventa.
- **Datos estructurados públicos:** además del RAG, el agente puede consultar (solo lectura) eventos en venta, precios, recintos y fechas mediante puertos de consulta acotados.
- **Seguridad:** endpoint con rate limiting; el texto del usuario y los chunks se tratan como **datos no confiables** (delimitados en el prompt, instrucciones del sistema fijas, sin herramientas de escritura); límite de longitud de entrada; no se revelan system prompt ni claves.
- **Configuración:** `OpenAI:ApiKey` (user-secrets/variable de entorno), `OpenAI:ChatModel` y `OpenAI:EmbeddingModel` configurables (verificar los nombres vigentes en la documentación de OpenAI). Embeddings por defecto: `text-embedding-3-small` → `vector(1536)`. **Cambiar de modelo de embeddings obliga a reindexar.**
- Cada consulta se registra en `logs_agente_ia` (consulta, respuesta, fuentes, tiempo, modelo, tokens, estado, `session_id`).

---

## 8. Seguridad, auditoría y logs

- JWT de acceso de vida corta (+ refresh token si se aprueba la decisión D-07 del plan). Policies: `Cliente`, `Administrador`, `DuenoRecinto`.
- Endpoints públicos: catálogo de eventos, detalle, mapa de recinto, agente de IA (con rate limit). Todo lo demás requiere autenticación.
- Rate limiting nativo (`AddRateLimiter`) en login, registro, compra y agente IA. CORS restringido al frontend.
- **Auditoría:** `IAuditLogger` escribe en `logs_auditoria` (usuario, entidad, `entidad_id`, acción, detalle JSONB, IP) para: login/logout/fallos, CRUD de entidades núcleo, compra, pago, reembolso, validación de boleto, moderación, exportación de reportes, ingesta RAG.
- **Nunca** loguear contraseñas, tokens JWT, tokens QR completos, API keys ni datos de tarjeta.
- Configuración sensible solo por user-secrets / variables de entorno. `.env` en `.gitignore`.

---

## 9. Reportería y BI

- 15 reportes (lista en `docs/PLAN_IMPLEMENTACION.md` §R) con filtros (fechas, evento, categoría, recinto, estado), agrupaciones y totales.
- `GET /api/v1/reportes/{codigo}?desde=&hasta=...&formato=json|xlsx|pdf`. Los reportes leen de **vistas `bi.vw_*`** (entidades *keyless* en EF Core).
- Exportación detrás de `IReportExporter` (ClosedXML / QuestPDF). Excel por streaming para volúmenes grandes. Cada exportación se audita.
- **Power BI** se conecta solo al esquema `bi` con el rol PostgreSQL de solo lectura `bi_reader`. Nunca a tablas operativas.

---

## 10. Pruebas

| Nivel | Qué cubre |
|---|---|
| Dominio / Aplicación (unitarias) | Reglas de negocio, handlers con puertos *fake* |
| Infraestructura (Testcontainers) | Repositorios EF, SQL atómico, RabbitMQ real, pgvector |
| API (`WebApplicationFactory`) | Contratos, auth/RBAC, códigos HTTP |
| Arquitectura (NetArchTest) | Regla de dependencia, controllers sin `DbContext` |
| **Concurrencia (obligatoria)** | N compradores simultáneos sobre cupo limitado ⇒ cero sobreventa |
| **Idempotencia (obligatoria)** | Mismo mensaje/`Idempotency-Key` dos veces ⇒ un solo efecto |

---

## 11. Git y entrega

- Ramas: `main` (protegida, solo PR) · `develop` · `feature/<fase>-<descripcion>` · `fix/...` · `docs/...`.
- **Conventional Commits**: `feat(ticketing): reservar cupo de forma atómica`, `fix(finance): ...`, `docs: ...`, `test: ...`, `refactor: ...`, `chore: ...`.
- Un PR por tarea o grupo pequeño de tareas; descripción con IDs del plan (`F5-03`), qué cambió y cómo se probó. Squash-merge para mantener historial legible.
- CI (GitHub Actions): build, test, format, reglas de arquitectura. PR rojo no se mergea.

---

## 12. Límites — NO hacer

- No editar una migración Flyway ya aplicada/mergeada: crea una nueva `V<n+1>__...sql`.
- No tocar el esquema `bi` sin migración. No dar a `bi_reader` permisos sobre tablas operativas.
- No usar `DateTime.Now`, `Thread.Sleep`, `.Result`/`.Wait()`, `async void`.
- No lógica de negocio en controllers, entidades EF anémicas con reglas dispersas, ni consultas SQL en controllers.
- No referenciar `Infrastructure` desde `Application`/`Domain`.
- No agregar paquetes NuGet sin justificarlo en el PR (y revisar licencia).
- No commitear secretos, `appsettings.*.json` con credenciales, ni binarios.
- No inventar tablas, columnas, endpoints ni comandos: consulta `docs/BASE_DE_DATOS.md` y el plan. Si falta algo, propónlo antes de implementarlo.
- No introducir Node.js/JavaScript ni FastAPI/Python en el backend.

---

## 13. Definition of Done (por tarea)

- [ ] Compila sin warnings (`dotnet build -warnaserror`) y `dotnet format` limpio.
- [ ] Pruebas nuevas/actualizadas y toda la suite en verde (incluye arquitectura).
- [ ] Reglas críticas de §5 respetadas (sobreventa, idempotencia, ledger, ownership).
- [ ] Endpoints documentados en Swagger, con ejemplos y códigos de error.
- [ ] Auditoría registrada donde corresponda (§8).
- [ ] Migración Flyway nueva si cambió el esquema (nunca editar una vieja).
- [ ] Casilla de la tarea marcada en `docs/PLAN_IMPLEMENTACION.md`.
- [ ] Commits convencionales y PR con descripción.

---

## 14. Frontend

Decisión pendiente (ver plan, D-09). Sea cual sea, consume la API REST versionada y el mapa de recintos usa `localidades.geometria` (JSON con polígonos) sobre `recintos.imagen_plano_url`. Esto **no** relaja la restricción del backend.
