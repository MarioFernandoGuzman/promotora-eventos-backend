# Plan de implementación — Promotora de Eventos (Backend)

Lista de tareas para construir el backend y **comprobar que se cumplen los requerimientos**. Se actualiza en cada PR: al terminar una tarea, marca su casilla `[x]` en el mismo commit.

**Cómo usarlo**
- Trabaja las fases en orden; cada una tiene **criterio de salida**.
- Cada tarea tiene un ID (`F5-03`) que se cita en commits y PRs.
- La **§1 Matriz de requerimientos** es el semáforo final: un requerimiento se da por cumplido solo cuando todas sus tareas están marcadas.

Referencias: [`AGENTS.md`](../AGENTS.md) · [`CLAUDE.md`](../CLAUDE.md) · [`docs/BASE_DE_DATOS.md`](BASE_DE_DATOS.md)

---

## 0. Decisiones

| ID | Decisión | Estado | Valor |
|---|---|---|---|
| D-01 | Backend | ✅ | **C# / .NET 10 LTS** (alternativa: .NET 8 cambiando `TargetFramework`) |
| D-02 | Arquitectura | ✅ | **Clean Architecture** (Domain · Application · Infrastructure · WebApi · Worker) |
| D-03 | Base de datos | ✅ | PostgreSQL 16 + pgvector |
| D-04 | Mensajería | ✅ | **RabbitMQ** (no Kafka) |
| D-05 | LLM y embeddings | ✅ | **OpenAI (ChatGPT)**; embeddings `text-embedding-3-small` → `vector(1536)` |
| D-06 | Migraciones | ✅ | SQL con Flyway; EF Core no genera migraciones |
| D-07 | ¿Refresh tokens además del JWT? | ⬜ pendiente | Recomendado: sí (+ tabla `refresh_tokens`) |
| D-08 | ¿Asientos numerados? | ⬜ pendiente | Por defecto no: solo localidades con cupo |
| D-09 | Tecnología del frontend | ⬜ pendiente | Opciones: Blazor (todo en .NET), React o Angular |
| D-10 | Pasarela de pago | ✅ | Adaptador simulado detrás de `IPaymentGateway` |
| D-11 | Quién asume IVA y comisión | ⬜ pendiente | Por defecto: comisión como cargo al cliente, IVA aparte |
| D-12 | Roles | ✅ | Un rol por usuario (`usuarios.rol` + perfil) |
| D-13 | Una compra = un evento | ✅ | `compras.evento_id` |
| D-14 | Envío de correo | ⬜ pendiente | MailHog en desarrollo; SMTP real (a definir) para la demo |

Las decisiones ⬜ deben cerrarse **antes** de la fase que las usa (D-08 y D-11 antes de F1; D-07 antes de F3; D-14 antes de F6; D-09 antes de F11).

---

## 1. Matriz de requerimientos → tareas

Marca ✅ solo cuando **todas** las tareas listadas estén completas y probadas.

| # | Requerimiento | Tareas que lo cubren | Estado |
|---|---|---|---|
| Q1 | CRUD completo de 12–15 entidades núcleo | F1-04, F3-*, F4-*, F5-*, F7-*, F8-*, F9-02, F12-02 | ⬜ |
| Q2 | Base de datos real (no SQLite) | F1-*, F2-02 | ⬜ |
| Q3 | Arquitectura formal, no un solo archivo | F2-01, F2-06, F12-03 | ⬜ |
| Q4 | Backend REST en tecnología permitida (.NET) | F2-01, F2-05 | ⬜ |
| Q5 | GitHub: ramas, PRs, historial legible | F0-04, F2-07 | ⬜ |
| Q6 | RabbitMQ en un flujo de negocio real | F6-* | ⬜ |
| Q7 | Agente de IA con información pública | F9-06, F9-07 | ⬜ |
| Q8 | RAG: ingesta, recuperación y citación | F9-01 … F9-05 | ⬜ |
| Q9 | Logs y auditoría (sistema + agente IA) | F3-06, F9-08, F1-07 | ⬜ |
| Q10 | ≥ 15 reportes con filtros y totales, exportables a Excel y PDF | F10-01 … F10-05 | ⬜ |
| Q11 | Integración con Power BI | F1-08, F10-06 | ⬜ |
| Q12 | Frontend funcional e integrado | F11-* | ⬜ |
| Q13 | Entregables: CLAUDE.md, AGENTS.md y Manual Técnico | F0-05, F12-04 | ⬜ |
| Q14 | Funcional: venta por localidad/general, alta demanda, finanzas, mapas, reseñas, 3 perfiles | F4-*, F5-*, F6-*, F7-*, F8-* | ⬜ |

---

## 2. Fases y tareas

### Fase 0 — Decisiones y arranque
- [ ] **F0-01** Cerrar decisiones D-07, D-08, D-09, D-11, D-14 (o fijar valores por defecto).
- [ ] **F0-02** Crear cuenta OpenAI y API key; guardarla fuera del repo (user-secrets).
- [ ] **F0-03** Crear repositorio GitHub, `.gitignore` (.NET + `.env`), licencia y README inicial.
- [ ] **F0-04** Configurar protección de `main`, rama `develop`, plantilla de PR y convención de commits.
- [ ] **F0-05** Commitear `AGENTS.md`, `CLAUDE.md`, `docs/BASE_DE_DATOS.md` y este plan.

**Salida:** repo creado, decisiones cerradas, archivos de agente versionados.

### Fase 1 — Base de datos v2
- [ ] **F1-01** Confirmar los ajustes menores ➕* de `BASE_DE_DATOS.md` §1.
- [ ] **F1-02** `V1__identidad.sql` y `V2__recintos.sql` (con `geometria` y `color`).
- [ ] **F1-03** `V3__catalogo_eventos.sql` (incluye `evento_presentadores`).
- [ ] **F1-04** `V4__ventas.sql` (compras con `public_id`, `idempotency_key`, `expira_en`; boletos con QR firmado).
- [ ] **F1-05** `V5__finanzas.sql` (pagos, cuentas, movimientos).
- [ ] **F1-06** `V6__resenas.sql`, `V7__auditoria_ia_rag.sql` (pgvector + HNSW), `V8__mensajeria.sql`.
- [ ] **F1-07** `V9__indices_triggers_permisos.sql`: índices, triggers de integridad (§6 de la BD), `REVOKE` en ledger y logs.
- [ ] **F1-08** `R__bi_vistas.sql`: vistas `bi.*` de los 15 reportes + rol `bi_reader`.
- [ ] **F1-09** `V10__seed_catalogos.sql` (categorías, cuenta PLATAFORMA, admin inicial) y `database/seed-dev/`.
- [ ] **F1-10** Validar: migrar desde cero en Docker, sin errores, `flyway info` limpio.

**Salida:** BD completa creada solo con migraciones; vistas `bi` consultables.

### Fase 2 — Esqueleto de la solución
- [ ] **F2-01** Crear solución con los 5 proyectos `src/` y los 5 de `tests/` con referencias correctas.
- [ ] **F2-02** `docker-compose.yml`: postgres+pgvector, rabbitmq (management), mailhog, flyway.
- [ ] **F2-03** `DbContext` con snake_case, `Pgvector`, entidades *keyless* para vistas `bi`.
- [ ] **F2-04** Base transversal: `Result<T>`, errores, `ICommandHandler/IQueryHandler`, pipeline de validación, `IClock`, `IUnitOfWork`.
- [ ] **F2-05** WebApi base: Swagger, `ProblemDetails`, versionado `/api/v1`, Serilog con CorrelationId, health checks.
- [ ] **F2-06** Pruebas de arquitectura (NetArchTest): regla de dependencia y controllers sin `DbContext`.
- [ ] **F2-07** CI en GitHub Actions: build, test, format, arquitectura.

**Salida:** `docker compose up`, migrar y levantar API con Swagger vacío; CI en verde.

### Fase 3 — Identidad y seguridad
- [ ] **F3-01** Registro de cliente (crea `usuarios` + `clientes`) y alta de admin/dueño por administrador.
- [ ] **F3-02** Login con JWT (+ refresh token si D-07 = sí), hash seguro de contraseñas.
- [ ] **F3-03** Policies RBAC: `Cliente`, `Administrador`, `DuenoRecinto` + verificación de propiedad.
- [ ] **F3-04** CRUD de usuarios/perfiles (baja = `INACTIVO`), cambio de contraseña.
- [ ] **F3-05** Rate limiting en login/registro; CORS.
- [ ] **F3-06** `IAuditLogger` y registro de LOGIN/LOGOUT/LOGIN_FAILED y CRUD de usuarios en `logs_auditoria`.
- [ ] **F3-07** Pruebas: acceso denegado por rol y por propiedad.

**Salida:** autenticación y autorización funcionando y auditadas.

### Fase 4 — Recintos, catálogo y mapas
- [ ] **F4-01** CRUD de `recintos` (dueño solo los suyos; admin todos).
- [ ] **F4-02** CRUD de `localidades` con `geometria` y `color`; validar suma de capacidades.
- [ ] **F4-03** Endpoint público del mapa: `GET /recintos/{id}/mapa` (plano + localidades con geometría y disponibilidad por evento).
- [ ] **F4-04** CRUD de `categorias_evento` y `presentadores`.
- [ ] **F4-05** CRUD de `eventos` (estados, `creado_por`, ventana de venta, `max_boletos_por_compra`).
- [ ] **F4-06** Gestión de `evento_presentadores`.
- [ ] **F4-07** CRUD de `tipos_entrada` (general / localidad / distancias de carrera) con validaciones de cupo.
- [ ] **F4-08** Listado público de eventos con filtros (categoría, ciudad, fecha, texto) y paginación.
- [ ] **F4-09** Subida de imágenes (plano, evento, presentador) con almacenamiento local detrás de un puerto.
- [ ] **F4-10** Auditoría de CRUD y pruebas de propiedad.

**Salida:** un dueño puede crear recinto, zonas, evento y tipos de entrada; el público ve catálogo y mapa.

### Fase 5 — Venta síncrona y boletos (primero sin colas)
- [ ] **F5-01** Dominio de compra: entidades, estados, cálculo de totales.
- [ ] **F5-02** Reserva atómica de cupo (`UPDATE` condicional) + devolución idempotente.
- [ ] **F5-03** `POST /compras` con `Idempotency-Key`, ventana de venta y límite por compra.
- [ ] **F5-04** Pago vía `IPaymentGateway` simulado (tasa de fallo configurable).
- [ ] **F5-05** Emisión de boletos con token QR firmado (HMAC) y datos del asistente (obligatorios en carreras).
- [ ] **F5-06** `GET /compras/{publicId}`, mis compras, mis boletos (con QR); cancelación.
- [ ] **F5-07** Validación en puerta: `POST /boletos/validar` (admin/dueño), atómica.
- [ ] **F5-08** Job/handler de expiración de compras `PENDIENTE`.
- [ ] **F5-09** **Prueba de concurrencia:** N hilos sobre cupo K ⇒ vendidos = K exactos.
- [ ] **F5-10** Prueba de idempotencia: misma `Idempotency-Key` ⇒ una sola compra.

**Salida:** compra completa y correcta de punta a punta, sin sobreventa bajo concurrencia.

### Fase 6 — Venta asíncrona con RabbitMQ
- [ ] **F6-01** Abstracciones `IMessagePublisher` y topología (exchanges, colas, retry, DLQ) declarada en código.
- [ ] **F6-02** Outbox: escritura transaccional y **relay** en `Worker` (`SKIP LOCKED`, confirmaciones).
- [ ] **F6-03** `POST /compras` pasa a **202 Accepted**: reserva + outbox; el cobro y la emisión ocurren en el consumidor.
- [ ] **F6-04** `ProcesarCompraConsumer`: idempotente (`mensajes_procesados`), ACK manual, reintentos con backoff, DLQ.
- [ ] **F6-05** Evento `compra.completada` → `EnviarConfirmacionConsumer` (correo + `notificaciones`).
- [ ] **F6-06** Flujo `usuario.registrado` → correo de bienvenida.
- [ ] **F6-07** Job de expiración en `Worker` con bloqueo entre instancias.
- [ ] **F6-08** Pruebas con Testcontainers (RabbitMQ real): duplicado, fallo → DLQ, caída del consumidor.
- [ ] **F6-09** Prueba de carga básica (p. ej. 500–1000 compras concurrentes) y evidencia de resultados.
- [ ] **F6-10** Documentar topología con diagrama.

**Salida:** el flujo de compra corre por colas, resiste duplicados y fallos, y queda evidencia en `notificaciones` y `outbox_mensajes`.

### Fase 7 — Finanzas
- [ ] **F7-01** Cuentas: alta automática de cuenta `DUENO` al crear dueño; cuenta `PLATAFORMA` única.
- [ ] **F7-02** Registro de `pagos` por intento y su estado.
- [ ] **F7-03** Movimientos al completar compra (`VENTA` al dueño, `COMISION` a plataforma) en la misma transacción.
- [ ] **F7-04** Reembolso: anula boletos, devuelve cupo, `REEMBOLSO` en el ledger.
- [ ] **F7-05** Liquidación a dueños (`LIQUIDACION`) con validación de saldo.
- [ ] **F7-06** Consultas: estado de cuenta por dueño, extracto de movimientos con filtros.
- [ ] **F7-07** Prueba de conciliación: `saldo == SUM(movimientos)` tras escenarios mixtos.
- [ ] **F7-08** Verificar que `UPDATE`/`DELETE` sobre el ledger falla (permisos/trigger).

**Salida:** el dinero cuadra siempre y cada centavo es trazable.

### Fase 8 — Reseñas
- [ ] **F8-01** Crear reseña: solo con boleto `USADO` del evento, una por (evento, cliente).
- [ ] **F8-02** Moderación por administrador (`APROBADA`/`RECHAZADA`).
- [ ] **F8-03** Listado público de reseñas aprobadas por evento.
- [ ] **F8-04** Agregados por presentador (promedio, cantidad) vía `evento_presentadores`.
- [ ] **F8-05** Editar/retirar reseña propia; auditoría.

**Salida:** reseñas moderadas y calificación por artista/equipo visible.

### Fase 9 — Agente de IA y RAG (OpenAI)
- [ ] **F9-01** Puertos `ILlmPort`, `IEmbeddingPort`, `IVectorSearchPort` y adaptadores OpenAI + pgvector.
- [ ] **F9-02** Ingesta: `POST /ia/documentos` (PDF/DOCX/TXT/MD) con extracción de texto; CRUD y versionado de `documentos_rag`.
- [ ] **F9-03** Chunking con solapamiento y guardado de página/orden.
- [ ] **F9-04** Embeddings en lote → `chunks_rag`; estados `PENDIENTE → INDEXADO/ERROR`; reindexación.
- [ ] **F9-05** Recuperación top-k con umbral y filtros; respuesta con **citas** (documento, página, chunk).
- [ ] **F9-06** Agente público `POST /ia/consultas`: prompt del sistema, respuesta "no lo sé" sin contexto, solo datos públicos.
- [ ] **F9-07** Consulta de datos estructurados públicos (eventos en venta, precios, recintos).
- [ ] **F9-08** `logs_agente_ia`: consulta, respuesta, fuentes, tiempos, modelo, tokens, estado.
- [ ] **F9-09** Defensas: rate limit, longitud máxima, tratamiento de contenido no confiable, pruebas de prompt injection.
- [ ] **F9-10** Documentos de negocio para ingestar (reglamento, reembolsos, FAQ, guía de carreras).
- [ ] **F9-11** Pruebas con LLM *fake* (deterministas) y una evaluación manual con preguntas de ejemplo.

**Salida:** el agente responde con fuentes citadas, se niega a lo que no sabe y deja trazas.

### Fase 10 — Reportería y BI
- [ ] **F10-01** Infraestructura de reportes: contrato común (filtros, agrupación, totales, paginación) y `GET /reportes/{codigo}`.
- [ ] **F10-02** Reportes R-01 … R-05.
- [ ] **F10-03** Reportes R-06 … R-10.
- [ ] **F10-04** Reportes R-11 … R-15.
- [ ] **F10-05** Exportadores Excel (ClosedXML) y PDF (QuestPDF) detrás de `IReportExporter`; auditoría de `EXPORT`.
- [ ] **F10-06** Power BI: cadena de conexión a `bi` con `bi_reader`, archivo `.pbix` de ejemplo y guía.
- [ ] **F10-07** Pruebas: totales de reportes coinciden con datos conocidos.

**Salida:** 15 reportes exportables y un tablero de Power BI funcionando.

### Fase 11 — Frontend e integración
- [ ] **F11-01** Definir stack del frontend (D-09) y crear proyecto.
- [ ] **F11-02** Autenticación (login/registro) y navegación por rol.
- [ ] **F11-03** Catálogo público de eventos con filtros y detalle.
- [ ] **F11-04** **Mapa del recinto** clicable (polígonos desde `geometria`) para elegir localidad.
- [ ] **F11-05** Flujo de compra con estado asíncrono (polling del 202) y mis boletos con QR.
- [ ] **F11-06** Panel de dueño/administrador: recintos, localidades, eventos, finanzas.
- [ ] **F11-07** Validación de boletos (lector/entrada de QR).
- [ ] **F11-08** Reseñas y moderación.
- [ ] **F11-09** Chat del agente de IA mostrando fuentes citadas.
- [ ] **F11-10** Pantalla de reportes con filtros y botones Excel/PDF.

**Salida:** el flujo completo se recorre desde la interfaz.

### Fase 12 — Cierre
- [ ] **F12-01** Revisión de seguridad (OWASP básico): inyección, autorización, secretos, CORS, rate limit.
- [ ] **F12-02** **Matriz CRUD** final (qué endpoints cubre cada entidad núcleo) incluida en el manual.
- [ ] **F12-03** Verificar arquitectura: reglas NetArchTest en verde y diagrama de capas actualizado.
- [ ] **F12-04** **Manual Técnico**: arquitectura, modelo de datos, flujos (compra asíncrona, RAG), despliegue, variables de entorno, pruebas, reportes, Power BI.
- [ ] **F12-05** README con arranque en 5 comandos y datos de demostración.
- [ ] **F12-06** Revisión de historial de Git (mensajes, PRs, ramas) y release `v1.0.0`.
- [ ] **F12-07** Ensayo de la demo: compra concurrente, cola, RAG con citas, reporte Excel/PDF, Power BI.

**Salida:** proyecto listo para entrega y defensa.

---

## R. Reportes requeridos (≥ 15)

Vistas y tablas de origen en `BASE_DE_DATOS.md` §10. Todos con filtros (fechas, evento, categoría, recinto, estado), agrupaciones y totales; exportables a Excel y PDF.

- [ ] **R-01** Ventas por evento
- [ ] **R-02** Ventas por categoría
- [ ] **R-03** Ventas por recinto
- [ ] **R-04** Ventas por rango de fechas y método de pago
- [ ] **R-05** Ocupación por localidad
- [ ] **R-06** Boletos vendidos vs. usados (asistencia)
- [ ] **R-07** Ingresos por dueño de recinto
- [ ] **R-08** Comisiones de la plataforma
- [ ] **R-09** Top clientes
- [ ] **R-10** Compras fallidas, canceladas y expiradas
- [ ] **R-11** Calificación promedio por presentador
- [ ] **R-12** Eventos por estado
- [ ] **R-13** Inscritos por distancia en carreras (5K/10K/21K/42K)
- [ ] **R-14** Uso del agente de IA (consultas, tiempos, tokens)
- [ ] **R-15** Auditoría de operaciones y notificaciones por estado

---

## 3. Lista de verificación final (antes de entregar)

- [ ] 15 entidades núcleo con CRUD (o baja lógica documentada) y pruebas
- [ ] PostgreSQL con pgvector; esquema creado solo con migraciones Flyway
- [ ] Clean Architecture verificada por pruebas de arquitectura
- [ ] Sin Node.js/JavaScript ni FastAPI en el backend
- [ ] Historial de Git legible: ramas, PRs, Conventional Commits
- [ ] RabbitMQ: flujo de compra con outbox, idempotencia, reintentos y DLQ demostrables
- [ ] Prueba de concurrencia: cero sobreventa
- [ ] Agente de IA + RAG con ingesta, recuperación y citación demostrables
- [ ] `logs_auditoria` y `logs_agente_ia` con datos reales
- [ ] 15 reportes con exportación Excel y PDF
- [ ] Power BI conectado al esquema `bi`
- [ ] Frontend integrado con el backend
- [ ] `AGENTS.md`, `CLAUDE.md` y Manual Técnico entregados
- [ ] Sin secretos en el repositorio

---

## 4. Riesgos principales

| Riesgo | Mitigación |
|---|---|
| Sobreventa por concurrencia | `UPDATE` atómico + prueba de concurrencia en CI (F5-09) |
| Mensajes duplicados o perdidos | Outbox + `mensajes_procesados` + ACK manual + DLQ |
| Costo/latencia de OpenAI | Embeddings en lote, `top-k` acotado, límites de tokens y rate limit; LLM *fake* en pruebas |
| Cambio de modelo de embeddings | La dimensión del vector queda fijada; reindexar si cambia |
| Alcance excesivo | Asientos numerados fuera del alcance (D-08); frontend mínimo viable primero |
| Pasarela real no disponible | Adaptador simulado detrás de puerto (D-10) |
| Licencias de paquetes | Revisar cada NuGet nuevo (ver `AGENTS.md` §2) |
