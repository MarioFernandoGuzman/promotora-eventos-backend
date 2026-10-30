# AGENTS.md — Promotora de Eventos (Backend)

> Fuente de verdad para cualquier agente de IA (ChatGPT/Codex, Claude Code, Cursor, Copilot...).
> Léelo completo antes de tocar código. Si algo contradice a otro documento, **este archivo manda**.
>
> - Modelo de datos completo: [`docs/BASE_DE_DATOS.md`](docs/BASE_DE_DATOS.md)
> - Tareas y estado de avance: [`docs/PLAN_IMPLEMENTACION.md`](docs/PLAN_IMPLEMENTACION.md)
> - Esquema histórico solo de referencia: [`docs/referencia/schema_v1.sql`](docs/referencia/schema_v1.sql). Nunca ejecutarlo ni moverlo a migraciones.
> - Las migraciones son la fuente de verdad del esquema implementado. Para tipos, claves o nulabilidad omitidos en el diccionario, seguir v1 con las correcciones v2; en tablas nuevas documentar cada suposición en el encabezado de la migración. F1-11 regeneró el diccionario completo desde V1–V9.

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
| LLM y embeddings | **Ollama Cloud** (`gpt-oss:120b`) para chat y **Gemini API** (`gemini-embedding-2`, 1536 dimensiones; nivel gratuito) para embeddings; HttpClient detrás de `ILlmPort` / `IEmbeddingPort` |
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
  Promotora.Infrastructure/   ADAPTADORES: EF Core, RabbitMQ, Ollama, SMTP, JWT, exportadores,
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
- **Los controllers nunca usan `DbContext`**, ni RabbitMQ, ni Ollama directamente: llaman a handlers de `Application`.
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
dotnet run --project src/Promotora.Worker      # host desde F2; consumidores, relay y jobs en F6

# Secretos (nunca en git)
dotnet user-secrets set "Ollama:ApiKey" "<tu-key>" --project src/Promotora.WebApi
dotnet user-secrets set "Ollama:ApiKey" "<tu-key>" --project src/Promotora.Worker
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
2. **Nunca borrar usuarios, datos financieros ni de compras.** Sin `DELETE` sobre `usuarios`, `compras`, `detalle_compra`, `boletos`, `pagos`, `movimientos_financieros`, `logs_*`. Se cambia el estado (`INACTIVO`, `CANCELADA`, `ANULADO`...). El rol de aplicación no tiene DELETE sobre usuarios; las FKs de perfiles, logs y notificaciones hacia usuarios son ON DELETE RESTRICT.
3. **El ledger es inmutable.** `movimientos_financieros` solo admite `INSERT`. Una corrección es un movimiento contrario. `cuentas_financieras.saldo` se modifica en la misma transacción que el movimiento y debe igualar `SUM(monto)`.
   Una compra completada registra VENTA (+subtotal, DUENO), COMISION (+comision_plataforma, PLATAFORMA) e IMPUESTO (+impuestos, IMPUESTOS); sus movimientos de cobro suman `compras.total` antes de reembolsos. El reembolso revierte los tres componentes. La comisión se cobra al cliente; el IVA se muestra aparte, con tasa configurable en appsettings y cálculo en Domain. Facturación electrónica fuera de alcance.
   Por moneda existe una única cuenta PLATAFORMA y una única IMPUESTOS (índices únicos parciales); se mantiene UNIQUE(dueno_id, moneda) y CHECK `(tipo='DUENO') = (dueno_id IS NOT NULL)`. Solo DUENO con saldo cero puede pasar de ACTIVA a CERRADA, registrando fecha_cierre; PLATAFORMA/IMPUESTOS no se cierran. Un trigger rechaza movimientos en cuentas CERRADA.
4. **Idempotencia.** `POST /api/v1/compras` exige header `Idempotency-Key` (se guarda en `compras.idempotency_key UNIQUE`; si se repite, se devuelve la compra existente). Los consumidores registran `mensajes_procesados(mensaje_id, consumidor)` en la misma transacción; si ya existe, hacen ACK y omiten.
5. **Una compra pertenece a un solo evento** (`compras.evento_id`); todos sus `tipos_entrada` deben ser de ese evento. Máximo `eventos.max_boletos_por_compra` boletos.
6. **Reserva con expiración.** Compra `PENDIENTE` reserva cupo y tiene `expira_en`. Si expira, falla o se cancela, el cupo se devuelve **una sola vez** (operación idempotente). El job de expiración solo toca PENDIENTE; cancelar por el usuario solo procede desde PENDIENTE (en PROCESANDO responde 409).
7. **Outbox.** Nunca publicar a RabbitMQ dentro de la transacción de negocio. Se escribe en `outbox_mensajes` en la misma transacción; el relay publica después.
8. **QR firmado.** `boletos.codigo_qr` es un token HMAC-SHA256 (no adivinable). Validar entrada = `UPDATE boletos SET estado='USADO', fecha_uso=now(), validado_por=@u WHERE codigo_qr=@c AND estado='VALIDO'` (0 filas ⇒ rechazado).
9. **Reseñas:** solo un cliente con boleto `USADO` de ese evento, una por (evento, cliente), nacen `PENDIENTE` y las modera un ADMINISTRADOR.
10. **Autorización por propiedad.** Un DUENO_RECINTO solo gestiona sus recintos/eventos/cuentas; un CLIENTE solo ve sus compras y boletos. Verificar propiedad en el handler, no solo el rol en el controller.
11. **Todo `TIMESTAMPTZ`; el código usa UTC** (`IClock`, nunca `DateTime.Now`).
12. **El agente de IA nunca accede a compras, usuarios ni finanzas.** Solo datos públicos y documentos RAG indexados.
13. **Transiciones condicionales.** Toda transición de estado es un UPDATE condicional `WHERE estado=<esperado>`. Solo quien gana (1 fila afectada) devuelve cupo, en la misma transacción. En reembolsos, el cupo devuelto es el número de boletos que pasaron de VALIDO a ANULADO.
14. **Perfil único obligatorio.** Cada usuario tiene exactamente una fila de perfil acorde a su rol; validar en aplicación y con constraint trigger `DEFERRABLE INITIALLY DEFERRED` que verifica la invariante al COMMIT.
15. **Política monetaria D-15.** Precios de tipos_entrada sin IVA. `subtotal = Σ(cantidad × precio_unitario)`; `comision = Redondear(subtotal × ComisionPorcentaje)`; `impuestos = Redondear((subtotal + comision) × TasaIva)`; `total = subtotal + comision + impuestos` (componentes ya redondeados). Todo con `decimal`, nunca `double`, y `MidpointRounding.AwayFromZero` a 2 decimales por componente. Configuración inicial: `Finanzas:TasaIva = 0.12` y `Finanzas:ComisionPorcentaje = 0.10`; comisión porcentual configurable, valor inicial y no dato legal. Application entrega una política tarifaria (record simple) a Domain; Domain no referencia configuración. Los montos se congelan en la compra. Reembolso solo total: revertir movimientos originales sin recalcular con tasas vigentes; parcial fuera de alcance. Total cero no genera movimientos (CHECK monto <> 0).

---

### Extensión aprobada de inventario numerado (D-08)

Las posiciones pertenecen a evento/localidad, no comparten ocupación entre eventos. Cada silla tiene una sola asignación vigente; mesa COMPLETA se reserva íntegra, genera un boleto por persona y cuenta personas para el límite de compra. Todas las sillas de una mesa comparten tipo/precio. Reserva, cupo agregado, asignaciones y outbox se confirman juntos; emitir, confirmar posiciones y registrar finanzas también. Liberar exige transición ganadora y asignación propietaria; boletos USADO nunca liberan silla. Publicar congela etiquetas/distribución. Esquema, rutas y compatibilidad aprobados en [LOCALIDADES_NUMERADAS.md](docs/LOCALIDADES_NUMERADAS.md); el esquema V12 está implementado y los endpoints/ciclo numerado siguen pendientes.

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
   Acuerdo F6-03/F6-04: asistentes opcionales por ítem, obligatorios en carreras. Se conservan en el payload del outbox existente por tipo_entrada; el relay publica solo publicId. No incluir esos datos en logs, RabbitMQ ni DLQ. Permanecen en PostgreSQL también después de publicar; no existe todavía una política automática de purga.
2. `Worker` (outbox relay, `FOR UPDATE SKIP LOCKED`) publica a `compra.procesar`.
3. Consumidor procesa, completa y emite `compra.completada`.
   Toma la compra con `UPDATE compras SET estado='PROCESANDO', procesando_desde=now() WHERE id=@id AND estado='PENDIENTE'`; 0 filas ⇒ ACK y omitir. La referencia ante la pasarela es `compras.public_id` para mantener el cobro idempotente.
4. Job de expiración marca `EXPIRADA` las `PENDIENTE` vencidas y devuelve cupo.
5. El cliente consulta `GET /api/v1/compras/{publicId}` (polling) hasta ver el estado final.
6. Job de recuperación (F6-11): si PROCESANDO supera el timeout desde procesando_desde (configurable, 5 minutos por defecto), consulta el pago por referencia public_id. Con pago APROBADO completa y emite boletos idempotentemente; si no, pasa condicionalmente a FALLIDA y devuelve cupo una sola vez. Probar la carrera procesar/expirar/cancelar.

La pasarela de pago es un **puerto**; el adaptador de API/Worker es `PasarelaSimuladaDurable`, con tasa de fallo configurable y diario INSERT-only `pagos_simulados` en V11 (F6-11 aprobado). Confirma el resultado en una conexión independiente antes de devolverlo. `FakePaymentGateway` queda como simulador puro para cálculo/pruebas. Recuperación solo consulta: nunca vuelve a cobrar; consulta fallida o respaldo histórico inconsistente conserva PROCESANDO sin devolver cupo. Solo ausencia confirmada en el simulador durable permite FALLIDA sin pago. Esto no constituye un protocolo de pasarela externa.

---

## 7. IA: agente público + RAG

- **Entrega F9:** carga y extracción por POST /api/v1/ia/documentos (PENDIENTE); indexación explícita por POST /api/v1/ia/documentos/{id}/reindexar, aprobada por el usuario y solo ADMINISTRADOR habilitado. Publica vectores/modelo y estado atómicamente, máximo 32 fragmentos; cloud fuera de la transacción. Reindexar un INDEXADO fallido conserva su índice anterior. La última versión publicada retira anteriores INDEXADO. El chat público es POST /api/v1/ia/consultas. Guía y límites en docs/IA_RAG.md.

- **Ingesta** (`POST /api/v1/ia/documentos`, solo ADMINISTRADOR): subir archivo → extraer texto → *chunking* (~800 tokens, solapamiento ~100) → embeddings en lote → `documentos_rag` + `chunks_rag`. Estados del documento: `PENDIENTE → INDEXADO | ERROR`.
- **Recuperación:** embedding de la pregunta → top-k por similitud coseno en `chunks_rag` (solo documentos `INDEXADO`) → umbral mínimo de similitud.
- **Generación con citación:** el prompt incluye los chunks con ID; la respuesta devuelve las fuentes (`documento`, `página`, `chunkId`) que se guardan en `logs_agente_ia.fuentes_citadas`. **Si no hay contexto suficiente, responde que no lo sabe**; no inventa.
- **Datos estructurados públicos:** además del RAG, el agente puede consultar (solo lectura) eventos en venta, precios, recintos y fechas mediante puertos de consulta acotados.
- **Seguridad:** endpoint con rate limiting; el texto del usuario y los chunks se tratan como **datos no confiables** (delimitados en el prompt, instrucciones del sistema fijas, sin herramientas de escritura); límite de longitud de entrada; no se revelan system prompt ni claves.
- **Configuración D-05 actualizada:** chat Ollama Cloud `gpt-oss:120b`; clave `OLLAMA_API_KEY` en `.env` local ignorado, introducida solo por el usuario (alternativas: variable de entorno o `Ollama:ApiKey` en user-secrets). Embeddings Gemini API `gemini-embedding-2` con 1536 dimensiones; GEMINI_API_KEY en el mismo .env ignorado. Solo endpoints estándar del nivel gratuito, sin facturación habilitada; el software no puede verificar el nivel de la cuenta. Backend exclusivamente C# con HttpClient. **Cambiar de modelo de embeddings obliga a reindexar aunque conserve la dimensión.** Pruebas automáticas sin llamadas reales ni consumo cloud. El asistente José responde respetuosamente en español solo con información pública de la promotora, eventos, recintos y reglamentos documentados; no inventa ni accede a datos privados.
- Cada consulta se registra en `logs_agente_ia` (consulta, respuesta, fuentes, tiempo, modelo, tokens, estado, `session_id`).

---

## 8. Seguridad, auditoría y logs

- JWT de acceso de vida corta + refresh tokens (D-07 confirmada). Policies: `Cliente`, `Administrador`, `DuenoRecinto`.
- `refresh_tokens` se crea en V1__identidad.sql (26 tablas originales; 27 con pagos_simulados aprobado en F6-11; 31 tras V12/EXT-NUM-02); definición completa en BASE_DE_DATOS.md §4.1. Solo guardar token_hash SHA-256, nunca el token en claro. Cada refresh rota el token; reutilizar uno revocado revoca toda la familia y se audita.
- El primer SUPERADMIN se crea al arrancar la API si no existe ninguno, con `Bootstrap:AdminEmail` y `Bootstrap:AdminPassword` desde user-secrets o variables de entorno; `usuarios.debe_cambiar_password = true` (columna NOT NULL DEFAULT false). Nunca usuarios ni credenciales en migraciones. V10__seed_catalogos.sql solo siembra categorías y cuentas PLATAFORMA/IMPUESTOS.
- Endpoints públicos: catálogo de eventos, detalle, mapa de recinto, agente de IA (con rate limit). Todo lo demás requiere autenticación.
- Rate limiting nativo (`AddRateLimiter`) en login, registro, compra y agente IA. CORS restringido al frontend.
- **Auditoría:** `IAuditLogger` escribe en `logs_auditoria` (usuario, entidad, `entidad_id`, acción, detalle JSONB, IP) para: login/logout/fallos, CRUD de entidades núcleo, compra, pago, reembolso, validación de boleto, moderación, exportación de reportes, ingesta RAG.
- **Nunca** loguear contraseñas, tokens JWT, tokens QR completos, API keys ni datos de tarjeta.
- `logs_auditoria` y `logs_agente_ia` son solo INSERT: REVOKE de UPDATE/DELETE al rol de aplicación y triggers que bloquean ambas operaciones; usuario_id nullable con ON DELETE RESTRICT.
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

- No editar una migración Flyway ya commiteada: crea una nueva `V<n+1>__...sql`.
- No tocar el esquema `bi` sin migración. No dar a `bi_reader` permisos sobre tablas operativas.
- No usar `DateTime.Now`, `Thread.Sleep`, `.Result`/`.Wait()`, `async void`.
- No lógica de negocio en controllers, entidades EF anémicas con reglas dispersas, ni consultas SQL en controllers.
- No referenciar `Infrastructure` desde `Application`/`Domain`.
- No agregar paquetes NuGet sin justificarlo en el PR (y revisar licencia).
- No commitear secretos, `appsettings.*.json` con credenciales, ni binarios.
- No inventar tablas, columnas, endpoints ni comandos: consulta `docs/BASE_DE_DATOS.md` y el plan. Si falta algo, propónlo antes de implementarlo.
- No introducir Node.js/JavaScript ni FastAPI/Python en el backend.
- No ejecutar el script histórico docs/referencia/schema_v1.sql. Los datos de prueba van en database/seed-dev/; ningún secreto o credencial de bootstrap en archivos versionados.

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

Decisión pendiente hasta la Fase 11 (ver plan, D-09). Sea cual sea, consume la API REST versionada y el mapa de recintos usa `localidades.geometria` (JSON con polígonos) sobre `recintos.imagen_plano_url`. Esto **no** relaja la restricción del backend.

## 15. Acuerdos de trabajo vigentes

- Ampliación D-08 aprobada el 29/10/2026: ejecutar EXT-NUM-01–EXT-NUM-07 antes de F10, en orden y por PRs pequeños según docs/LOCALIDADES_NUMERADAS.md. Localidades generales y numeradas por evento; mesas COMPLETA/POR_SILLA, QR por persona y precio como suma de sillas. Esta autorización reemplaza la pausa posterior a F9 solo para este bloque; reportes/Power BI siguen pendientes. Estructura publicada inmutable, reserva atómica por silla, historial de asignaciones y liberación por propietario de reserva; API y Worker actualizados antes de habilitar ventas numeradas. Conservar cambios ajenos del frontend; no incorporarlos sin revisión. Pruebas focalizadas y suite completa al cierre.

- El usuario autoriza terminar F9-03–F9-11 y luego detener implementación: reportes/Power BI se retoman después. Solicita análisis de endpoints, vistas y flujos para frontend, sin elegir aún D-09 ni crear interfaz. Aprueba POST /api/v1/ia/documentos/{id}/reindexar. Una tarea/commit en orden sobre la rama de Fase 9 existente, sin push/merge. Pruebas focalizadas por cambio y una suite completa al cierre; repetir solo ante cambios, fallos o dudas concretas. Evaluación cloud acotada del cierre: dos embeddings y una respuesta con texto público, sin reintentos ni cambios de contraseña del administrador local.

- Excepción expresamente solicitada por el usuario el 13/10/2026: crear su .env privado local con las credenciales proporcionadas/existentes y utilizarlas para una prueba pequeña de embeddings y una conversación. No mostrarlas ni versionarlas; no ampliar esa autorización a divulgar claves. Start-Local.ps1 carga los valores permitidos para API/Worker sin repetir contraseñas y restaura el entorno al salir. El acceso local RabbitMQ queda limitado a recursos de esta aplicación, sin rol administrator. Esta excepción no altera el orden de F9 ni autoriza publicar secretos o activar pagos.

- Restricción de costo confirmada por el usuario: solo cuotas/créditos incluidos sin activar facturación ni recargas. Gemini Embedding 2 tiene Free Tier estándar; Ollama Free limita acceso a modelos starter y no se debe prometer que gpt-oss:120b esté incluido en toda cuenta. Si requiere comprar créditos, detener la prueba real de chat y proponer una alternativa gratuita; no cambiar modelo silenciosamente ni activar pagos. El backend no puede comprobar el nivel de facturación asociado a una clave.

- Fase 9 desbloqueada por elección del usuario: Ollama Cloud con gpt-oss:120b, `.env` local y mínimo consumo en pruebas. Corrección expresa del usuario: todo por API key, sin instalar Ollama ni descargar modelos. Embeddings con Gemini API gemini-embedding-2 a 1536 dimensiones, segunda clave en .env y proyecto Free Tier sin facturación; no usar Batch API diferida de pago. Se conserva el orden F9-01–F9-11 y la arquitectura; F9-01 no habilita todavía el endpoint de chat. Nunca leer ni mostrar la clave real en herramientas o logs; el usuario la introduce fuera del control de versiones.

- Fase 8 aprobada en un PR, F8-01–F8-05 en orden y un commit por tarea. Comentario opcional máximo 510 caracteres, calificación 1–5. POST /api/v1/resenas (CLIENTE con boleto USADO, una por evento/cliente, PENDIENTE), GET /api/v1/resenas/mis (CLIENTE), GET /api/v1/resenas y PATCH /api/v1/resenas/{id}/moderar (ADMINISTRADOR). Públicas: GET /api/v1/eventos/{id}/resenas (APROBADA) y GET /api/v1/presentadores/{id}/calificacion (promedio/cantidad). PUT /api/v1/resenas/{id} y POST /api/v1/resenas/{id}/retirar solo autor: editar retorna PENDIENTE, retirar usa RECHAZADA sin borrar, permite editar rechazada/retirada para revisión. Moderar solo desde PENDIENTE. Listados paginados, cambios condicionales y auditoría, sin nuevo esquema. Pausa histórica previa a Fase 9 resuelta por la elección de Ollama en D-05.

- Bloque F7-06–F7-08 aprobado en un PR, una tarea/commit y en orden. F7-06: GET /api/v1/cuentas (filtros duenoId/moneda/tipo/estado), GET /api/v1/cuentas/{id}, GET /api/v1/cuentas/{id}/movimientos (tipo/compraId/desde/hasta). ADMINISTRADOR todas; DUENO_RECINTO solo propias, ajena/inexistente 404, filtros ajenos del listado producen vacío; CLIENTE 403. Paginación default 20/máximo 100, page 1–100000. Extracto por fecha e ID descendentes, desde UTC inclusivo/hasta exclusivo. F7-07 prueba escenarios financieros mixtos y F7-08 permisos/triggers del ledger en bases temporales; sin cambio de esquema.

- F7-05 aprobado: POST /api/v1/cuentas/{id}/liquidar, solo ADMINISTRADOR habilitado, body monto positivo con dos decimales y header Idempotency-Key obligatorio (ASCII alfanumérico o . _ : -, 1–128 caracteres, sensible a mayúsculas, por cuenta). Solo DUENO ACTIVA con saldo suficiente y conciliado; LIQUIDACION negativa con compra_id NULL, saldo y auditoría en la misma transacción. Referencia reservada liquidacion:v1:<SHA-256 de la clave> en descripcion, búsqueda/inserción bajo bloqueo de cuenta sin nueva columna/índice. Repetir mismo monto devuelve 200 original incluso tras cierre; distinto monto con la misma clave devuelve 409. Registro contable simulado, sin transferencia externa.

- F7-04 aprobado: POST /api/v1/compras/{publicId}/reembolsar sin body, solo ADMINISTRADOR habilitado. Reembolso total con movimientos originales en las mismas cuentas, saldo suficiente y cuentas afectadas ACTIVA (si no, 409). Pago APROBADO → REEMBOLSADO; compra permanece COMPLETADA. Solo VALIDO → ANULADO devuelve cupo; USADO mantiene historial aunque se reembolse todo. Repetición consistente devuelve 200 sin nuevos efectos. Alcance financiero simulado, sin transferencia externa ni modificación del diario inmutable del proveedor.

- Excepción de orden aprobada después de F6-07: adelantar F7-01 → F7-02 → F7-03, una tarea por commit, para desbloquear finalización financiera y pruebas completas; después retomar pendientes de F6-03/F6-04 y F6-08 → F6-09 → F6-10 → F6-11, cerrar salidas F5/F6 y continuar F7-04. No marcar tareas financieras antes de completarlas ni sustituir pruebas de compra pagada por efectos artificiales.

- Una tarea del plan a la vez y en orden de fases. Antes de cada tarea, describir en 3–5 líneas el trabajo y los archivos; esperar «ok» si toca esquema, seguridad, mensajería o dinero.
- D-08: venta mixta general/numerada según docs/LOCALIDADES_NUMERADAS.md; implementación pendiente del bloque EXT-NUM. D-14: MailHog en desarrollo. OLLAMA_API_KEY y GEMINI_API_KEY las introduce el usuario en .env local ignorado o configuración externa; el agente nunca escribe la clave real.
- Ramas desde develop actualizado: documentación en docs/f1-aclaraciones; PR-B F1-02/F1-03 en feature/f1-identidad-recintos-catalogo; PR-C F1-04 a F1-06 en feature/f1-ventas-finanzas-ia-mensajeria; PR-D F1-07 a F1-10 y F1-11 (después de F1-07) en feature/f1-integridad-bi-seed. Cada PR empieza después de que el usuario mergee el anterior y actualice develop.
- Solo commits locales, pequeños y en español, Conventional Commits y cuerpo `Refs: <ID>`. Mostrar git status y mensaje antes de cada commit. Nunca push, merge ni modificar main/develop. El usuario gestiona push y PR.
- Marcar la tarea en el plan en el mismo commit; ejecutar build/test/format cuando existan y apliquen. En F1-10 no existe docker-compose.yml: usar contenedor temporal pgvector/pgvector:pg16 y flyway/flyway sobre database/migrations con el comando de .github/workflows/ci.yml.
- No ejecutar comandos destructivos sin confirmación. Al cerrar la fase, revisar el diff contra el criterio de salida y §§5/12, listar incumplimientos y preparar título/descripción de PR según .github/pull_request_template.md.
