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
| D-07 | ¿Refresh tokens además del JWT? | ✅ | Sí; tabla `refresh_tokens`, rotación y revocación de familia ante reutilización |
| D-08 | ¿Asientos numerados? | ✅ | No: solo localidades con cupo |
| D-09 | Tecnología del frontend | ⬜ pendiente hasta Fase 11 | Opciones: Blazor (todo en .NET), React o Angular |
| D-10 | Pasarela de pago | ✅ | Adaptador simulado detrás de `IPaymentGateway` |
| D-11 | Quién asume IVA y comisión | ✅ | Comisión como cargo al cliente; IVA aparte, tasa configurable y cálculo en Domain; cuenta IMPUESTOS (BASE_DE_DATOS.md §4.5) |
| D-12 | Roles | ✅ | Un rol por usuario (`usuarios.rol` + perfil) |
| D-13 | Una compra = un evento | ✅ | `compras.evento_id` |
| D-14 | Envío de correo | ✅ | MailHog en desarrollo |
| D-15 | Política monetaria de compra | ✅ | Precios sin IVA; `Finanzas:TasaIva = 0.12`, `Finanzas:ComisionPorcentaje = 0.10` (valor inicial configurable, no dato legal). Comisión sobre subtotal, IVA sobre subtotal + comisión; decimal y redondeo AwayFromZero por componente a 2 decimales; montos congelados y reembolso solo total |

Las decisiones D-07, D-08, D-11 y D-14 están confirmadas. D-09 se resolverá en F11-01. F0-01 está cerrada con este aplazamiento explícito; F0-02 permanece pendiente de confirmación del usuario, quien gestiona OpenAI:ApiKey con user-secrets.

El modelo objetivo tiene **26 tablas**, incluida `refresh_tokens`. Las migraciones son la fuente de verdad del esquema implementado. Para tipos, claves o nulabilidad omitidos de tablas existentes se usa [`referencia/schema_v1.sql`](referencia/schema_v1.sql) con las correcciones v2; nunca se ejecuta ese script ni se mueve a migraciones. Para tablas nuevas se sigue BASE_DE_DATOS.md y se registra cada suposición en el encabezado de la migración. F1-11 regeneró el diccionario completo desde V1–V9.

Entrega de Fase 1: PR-B (F1-02/F1-03) en `feature/f1-identidad-recintos-catalogo`; PR-C (F1-04 a F1-06) en `feature/f1-ventas-finanzas-ia-mensajeria`; PR-D (F1-07 a F1-10 y F1-11, ejecutada después de F1-07) en `feature/f1-integridad-bi-seed`. Cada rama parte de develop actualizado después de que el usuario mergee el PR anterior. Solo commits locales en español, con `Refs: <ID>` en el cuerpo, mostrando git status y mensaje antes de cada commit; sin push ni merge del agente y sin modificar main/develop.

---

## 1. Matriz de requerimientos → tareas

Marca ✅ solo cuando **todas** las tareas listadas estén completas y probadas.

| # | Requerimiento | Tareas que lo cubren | Estado |
|---|---|---|---|
| Q1 | CRUD completo de 12–15 entidades núcleo | F1-04, F3-*, F4-*, F5-*, F7-*, F8-*, F9-02, F12-02 | ⬜ |
| Q2 | Base de datos real (no SQLite) | F1-*, F2-02 | ✅ |
| Q3 | Arquitectura formal, no un solo archivo | F2-01, F2-06, F12-03 | ⬜ |
| Q4 | Backend REST en tecnología permitida (.NET) | F2-01, F2-05 | ✅ |
| Q5 | GitHub: ramas, PRs, historial legible | F0-04, F2-07 | ✅ |
| Q6 | RabbitMQ en un flujo de negocio real y recuperación de compras atascadas | F6-01 … F6-11 (incluye recuperación y prueba de carrera) | ⬜ |
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
- [x] **F0-01** Cerrar D-07, D-08, D-11 y D-14; aplazar explícitamente D-09 hasta F11-01.
- [ ] **F0-02** Crear cuenta OpenAI y API key; guardarla fuera del repo (user-secrets).
- [x] **F0-03** Crear repositorio GitHub, `.gitignore` (.NET + `.env`), licencia y README inicial.
- [x] **F0-04** Configurar protección de `main`, rama `develop`, plantilla de PR y convención de commits.
- [x] **F0-05** Commitear `AGENTS.md`, `CLAUDE.md`, `docs/BASE_DE_DATOS.md` y este plan.

**Salida:** repo creado, decisiones cerradas salvo D-09 aplazada hasta F11-01, archivos de agente versionados; F0-02 requiere confirmación del usuario.

### Fase 1 — Base de datos v2
- [x] **F1-01** Confirmar los ajustes menores ➕* de `BASE_DE_DATOS.md` §1.
- [x] **F1-02** `V1__identidad.sql` (incluye `refresh_tokens`, `usuarios.debe_cambiar_password` y FKs a usuarios RESTRICT) y `V2__recintos.sql` (con `geometria` y `color`).
- [x] **F1-03** `V3__catalogo_eventos.sql` (incluye `evento_presentadores`).
- [x] **F1-04** `V4__ventas.sql` (compras con `public_id`, `idempotency_key`, `expira_en`, `procesando_desde TIMESTAMPTZ NULL`; boletos preparados para QR firmado).
- [x] **F1-05** `V5__finanzas.sql` (pagos, cuentas PLATAFORMA/DUENO/IMPUESTOS con estado ACTIVA/CERRADA y fecha_cierre, movimientos VENTA/COMISION/IMPUESTO/LIQUIDACION/REEMBOLSO; CHECK de dueño y UNIQUE(dueno_id, moneda)).
- [x] **F1-06** `V6__resenas.sql`, `V7__auditoria_ia_rag.sql` (pgvector + HNSW), `V8__mensajeria.sql`.
- [x] **F1-07** `V9__indices_triggers_permisos.sql`: índices (refresh por usuario/familia, compras por procesando_desde WHERE estado='PROCESANDO', únicos parciales PLATAFORMA/IMPUESTOS por moneda), triggers de integridad (§6 de la BD), constraint trigger de perfil DEFERRABLE INITIALLY DEFERRED al COMMIT, bloqueo de movimientos en cuentas CERRADA, triggers contra UPDATE/DELETE en ledger y ambos logs; REVOKE correspondiente y de DELETE sobre usuarios y datos de compras/financieros.
- [x] **F1-11** Regenerar el diccionario completo en `BASE_DE_DATOS.md` (tipo, PK/FK, nulabilidad, default, CHECK) a partir de las migraciones. Ejecutar después de F1-07 y antes de F1-08.
- [x] **F1-08** `R__bi_vistas.sql`: vistas `bi.*` de los 15 reportes + rol `bi_reader`.
- [x] **F1-09** `V10__seed_catalogos.sql` (solo categorías y cuentas PLATAFORMA e IMPUESTOS; nunca admin ni credenciales) y datos de prueba en `database/seed-dev/`, fuera de migraciones de producción y sin secretos.
- [x] **F1-10** Validar: migrar desde cero en un contenedor temporal Docker `pgvector/pgvector:pg16` con `flyway/flyway` sobre `database/migrations`, usando el mismo comando de `.github/workflows/ci.yml`; sin errores, `flyway info` limpio. No usar docker-compose.yml, que llega en F2-02.

**Salida:** BD completa (26 tablas) creada solo con migraciones; vistas `bi` consultables y diccionario completo regenerado desde las migraciones.

### Fase 2 — Esqueleto de la solución
- [x] **F2-01** Crear solución con los 5 proyectos `src/` y los 5 de `tests/` con referencias correctas.
- [x] **F2-02** `docker-compose.yml`: postgres+pgvector, rabbitmq (management), mailhog, flyway.
- [x] **F2-03** `DbContext` con snake_case, `Pgvector`, entidades *keyless* para vistas `bi`.
  - Corrección posterior: sentinel `false` explícito en `usuarios.debe_cambiar_password` y `outbox_mensajes.publicado`; pruebas PostgreSQL de ambos valores con el warning de EF convertido en error. Sin cambio de esquema ni migraciones.
- [x] **F2-04** Base transversal: `Result<T>`, errores, `ICommandHandler/IQueryHandler`, pipeline de validación, `IClock`, `IUnitOfWork`.
- [x] **F2-05** WebApi base: Swagger, `ProblemDetails`, versionado `/api/v1`, Serilog con CorrelationId, health checks.
- [x] **F2-06** Pruebas de arquitectura (NetArchTest): regla de dependencia y controllers sin `DbContext`.
- [x] **F2-07** CI en GitHub Actions: build, test, format, arquitectura.

**Salida:** `docker compose up`, migrar y levantar API con Swagger vacío; CI en verde.

Implementación y validación local de F2-01–F2-07 documentadas en [`VALIDACION_FASE_2.md`](VALIDACION_FASE_2.md). La ejecución remota de GitHub Actions debe confirmarse al publicar el PR; el agente no hace push ni merge.

### Fase 3 — Identidad y seguridad
- [x] **F3-01** Registro de cliente (crea `usuarios` + `clientes`) y alta de admin/dueño por administrador. Al arrancar la API, si no existe ningún SUPERADMIN, crear el primero leyendo `Bootstrap:AdminEmail` y `Bootstrap:AdminPassword` desde user-secrets o variables de entorno, con `debe_cambiar_password = true`; nunca en migraciones ni con credenciales en el repo.
- [x] **F3-02** Login con JWT y refresh tokens: hash SHA-256 del token, rotación en cada refresh; reutilizar uno revocado revoca toda la familia y se audita. Contraseñas con hash seguro con sal; exigir cambio cuando debe_cambiar_password sea true.
- [x] **F3-03** Policies RBAC: `Cliente`, `Administrador`, `DuenoRecinto` + verificación de propiedad.
- [x] **F3-04** CRUD de usuarios/perfiles (baja = `INACTIVO`), cambio de contraseña.
- [x] **F3-05** Rate limiting en login/registro; CORS.
- [x] **F3-06** `IAuditLogger` y registro de LOGIN/LOGOUT/LOGIN_FAILED y CRUD de usuarios en `logs_auditoria`.
- [x] **F3-07** Pruebas: acceso denegado por rol y por propiedad.

**Salida:** autenticación y autorización funcionando y auditadas.

Validación local: 82 pruebas en verde, build Release sin warnings, format limpio y Compose/Flyway/Kestrel
con bootstrap y cambio obligatorio verificados; ver [VALIDACION_FASE_3.md](VALIDACION_FASE_3.md).
GitHub Actions confirma el CI de este bloque al publicar el PR.

Guía de integración para el equipo frontend: [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md), con acceso autenticado a Swagger, cambio inicial, rotación, logout, permisos y CORS de F3-02–F3-05.

### Fase 4 — Recintos, catálogo y mapas
- [x] **F4-01** CRUD de `recintos` (dueño solo los suyos; admin todos).
- [x] **F4-02** CRUD de `localidades` con `geometria` y `color`; validar suma de capacidades.
- [x] **F4-03** Endpoint público del mapa: `GET /recintos/{id}/mapa` (plano + localidades con geometría y disponibilidad por evento).
- [x] **F4-04** CRUD de `categorias_evento` y `presentadores`.
- [x] **F4-05** CRUD de `eventos` (estados, `creado_por`, ventana de venta, `max_boletos_por_compra`).
- [x] **F4-06** Gestión de `evento_presentadores`.
- [x] **F4-07** CRUD de `tipos_entrada` (general / localidad / distancias de carrera) con validaciones de cupo.
- [x] **F4-08** Listado público de eventos con filtros (categoría, ciudad, fecha, texto) y paginación.
- [x] **F4-09** Subida de imágenes (plano, evento, presentador) con almacenamiento local detrás de un puerto.
- [x] **F4-10** Auditoría de CRUD y pruebas de propiedad.

**Salida:** un dueño puede crear recinto, zonas, evento y tipos de entrada; el público ve catálogo y mapa.

Primer bloque F4-01–F4-03 implementado: 93 pruebas en verde y revisión en [VALIDACION_F4_RECINTOS_MAPA.md](VALIDACION_F4_RECINTOS_MAPA.md). Segundo bloque F4-04–F4-08 implementado: 111 pruebas en verde y 49 operaciones Swagger; guía en [CATALOGO_EVENTOS.md](CATALOGO_EVENTOS.md) y revisión en [VALIDACION_F4_CATALOGO_EVENTOS.md](VALIDACION_F4_CATALOGO_EVENTOS.md). F4-09/F4-10 completadas: 120 pruebas en verde y 53 operaciones Swagger. Imágenes en [IMAGENES.md](IMAGENES.md), criterio de salida y revisión de fase en [VALIDACION_FASE_4.md](VALIDACION_FASE_4.md). Fase 4 cerrada localmente; CI remoto se confirma al publicar el PR.

### Fase 5 — Venta síncrona y boletos (primero sin colas)

**D-15 confirmada:** `subtotal = Σ(cantidad × precio_unitario)`; `comision = Redondear(subtotal × ComisionPorcentaje)`; `impuestos = Redondear((subtotal + comision) × TasaIva)`; `total = subtotal + comision + impuestos`, sumando componentes ya redondeados. Todo usa `decimal`, nunca `double`; redondeo a 2 decimales con `MidpointRounding.AwayFromZero`. Los precios de tipos_entrada no incluyen IVA. Domain recibe una política tarifaria (record simple) desde Application y no referencia configuración. Tasas configurables en appsettings, inicialmente IVA 0.12 y comisión porcentual 0.10; no se consideran asesoría ni definición legal. Los precios/componentes quedan congelados en la compra: el reembolso total revierte los movimientos originales sin recalcular con tasas vigentes. Con total cero no se generan movimientos (CHECK monto <> 0). Reembolso parcial fuera de alcance.

Pruebas obligatorias de F5-01: subtotal 300.00 → comisión 30.00, IVA 39.60, total 369.60; 33.33 → 3.33, 4.40, 41.06; 0.00 → todos cero; además, invariante subtotal + comisión + impuestos = total sobre una serie de subtotales aleatorios.
- [x] **F5-01** Dominio de compra: entidades, estados condicionales, cálculo de totales; comisión como cargo al cliente e IVA aparte, tasa configurada en appsettings y cálculo en Domain.
  - D-15 registrada antes del código. Reglas, configuración, límites y pruebas en [COMPRAS_DOMINIO.md](COMPRAS_DOMINIO.md). Validación de F5-01: 137 pruebas en verde, build sin warnings y format limpio. F5-02/F5-03 incorporaron posteriormente reserva persistida y endpoint.
- [x] **F5-02** Reserva atómica de cupo (`UPDATE` condicional) + devolución idempotente.
  - Puerto y adaptador transaccionales; pruebas PostgreSQL de concurrencia, estados y rollback. Diseño y comandos en [RESERVA_CUPO.md](RESERVA_CUPO.md).
- [x] **F5-03** `POST /compras` con `Idempotency-Key`, ventana de venta y límite por compra.
  - HTTP 201 nueva / 200 repetida propia; compra PENDIENTE, reserva + detalles + auditoría transaccionales. D-15 congelada, validación del actor y padres activos, límite agregado y carreras de clave. 159 pruebas Release en verde; 54 operaciones Swagger. Guía frontend/local en [COMPRAS_API.md](COMPRAS_API.md) y revisión del bloque en [VALIDACION_F5_COMPRAS.md](VALIDACION_F5_COMPRAS.md). Componentes de pago/emisión F5-04/F5-05 implementados, integración financiera pendiente de F7; handler de expiración en F5-08, programación automática en F6-07 y contrato 202/outbox en F6-03.
- [x] **F5-04** Pago vía `IPaymentGateway` simulado (tasa de fallo configurable).
  - Puerto y singleton en API/Worker; referencia public_id, monto congelado, repetición concurrente, consulta y rechazo configurable. Diseño, comandos y límites en [PAGOS_SIMULADOS.md](PAGOS_SIMULADOS.md). El POST aún no cobra: integración transaccional con pagos/ledger depende de F7-02/F7-03.
- [x] **F5-05** Emisión de boletos con token QR firmado (HMAC) y datos del asistente (obligatorios en carreras).
  - Componente interno transaccional, emisión concurrente idempotente y auditoría; exige compra COMPLETADA con respaldo financiero consistente. Firma con clave externa y categoría de carreras identificada por ID configurable. 187 pruebas Release en verde. Configuración y pruebas en [BOLETOS_EMISION.md](BOLETOS_EMISION.md); revisión en [VALIDACION_F5_PAGO_BOLETOS.md](VALIDACION_F5_PAGO_BOLETOS.md). La finalización HTTP sigue pendiente hasta F7-02/F7-03 según la dependencia documentada abajo.
- [x] **F5-06** `GET /compras/{publicId}`, mis compras, mis boletos (con QR); cancelación por usuario solo desde PENDIENTE mediante UPDATE condicional; en PROCESANDO responde 409. Solo el ganador devuelve cupo en la misma transacción.
  - Consultas propias paginadas, QR sin caché, compra ajena 404, cancelación repetida idempotente y auditoría UPDATE transaccional. Pruebas de veinte cancelaciones concurrentes, propiedad, paginación y rollback; guía y prueba local en [COMPRAS_API.md](COMPRAS_API.md).
- [x] **F5-07** Validación en puerta: `POST /boletos/validar` (admin/dueño), atómica.
  - Firma HMAC, propiedad del recinto y compra COMPLETADA; UPDATE condicional VALIDO → USADO con fecha_uso/validado_por y auditoría VALIDATE transaccional. Veinte escaneos con dos administradores permiten una sola entrada. Contratos, roles, errores y comandos separados en [BOLETOS_VALIDACION.md](BOLETOS_VALIDACION.md).
- [x] **F5-08** Job/handler de expiración solo de compras `PENDIENTE`, con UPDATE condicional y devolución de cupo una sola vez por el ganador, en la misma transacción.
  - Handler interno por lotes registrado en API/Worker, sin temporizador ni endpoint; activación periódica/coordinación de instancias en F6-07. Auditoría UPDATE y devolución en la misma transacción; pruebas de repetición, carreras y rollback. Guía local: [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md).
- [ ] **F5-09** **Prueba de concurrencia:** N hilos sobre cupo K ⇒ vendidos = K exactos.
  - En el flujo actual se comprueban reservas PENDIENTE: ocho clientes HTTP distintos, casos 40/7, 40/8 y 33/9, cantidades 1/2/3; cupo final cero, cantidades exactas, rechazo sin compras parciales y una auditoría por ganadora. Evidencia: [PRUEBAS_F5_CONCURRENCIA.md](PRUEBAS_F5_CONCURRENCIA.md). La comprobación de ventas COMPLETADA/pagos/ledger/boletos permanece pendiente al integrar F7-02/F7-03 antes de cumplir la salida de Fase 5.
- [x] **F5-10** Prueba de idempotencia: misma `Idempotency-Key` ⇒ una sola compra.
  - Cuarenta solicitudes HTTP con una clave propia, disputa entre clientes, reintento tras falta de cupo y reutilización tras cancelar/cambiar precio: una compra/detalle/auditoría y una sola reserva. Evidencia: [PRUEBAS_F5_IDEMPOTENCIA.md](PRUEBAS_F5_IDEMPOTENCIA.md). Consumidores en F6 y finalización/ledger/emisión se validarán al integrar F7; salida de Fase 5 y F5-09 pendientes.

**Salida:** compra completa y correcta de punta a punta, sin sobreventa bajo concurrencia.

**Dependencia confirmada con el usuario:** mantener el orden y preparar F5-04/F5-05 como componentes. No habilitar finalización HTTP sin registro financiero: AGENTS.md §5 exige ledger en la misma transacción de COMPLETADA. La conexión del flujo a POST y la salida de Fase 5 quedan pendientes hasta integrar F7-02/F7-03. No marcar anticipadamente tareas de F7. La consulta en memoria del pago simulado también requiere respaldo durable antes de habilitar F6-11; un resultado desconocido tras reinicio no prueba rechazo ni ausencia de pago.

### Fase 6 — Venta asíncrona con RabbitMQ
- [x] **F6-01** Abstracciones `IMessagePublisher` y topología (exchanges, colas, retry, DLQ) declarada en código.
  - RabbitMQ.Client 7.2.2 async v7 bajo Apache 2.0, configuración externa, canal serializado, persistencia y confirms con mandatory. Cinco pruebas RabbitMQ real; 221 pruebas Release en verde, build sin warnings y format limpio. Retry con TTL por mensaje para backoff en F6-04. Guía: [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). Sin consumidores ni cambios HTTP todavía.
- [x] **F6-02** Outbox: escritura transaccional y **relay** en `Worker` (`SKIP LOCKED`, confirmaciones).
  - Writer scoped exige transacción de negocio; relay independiente por mensaje, UPDATE condicional tras confirms, intentos persistidos y reintento con ID estable. Worker periódico configurable, opt-in y shutdown async. Siete pruebas PostgreSQL+RabbitMQ (rollback, concurrencia, fallo/retorno, caída antes de commit y host real); 228 pruebas Release en verde, build sin warnings y format limpio. Guía y comandos separados: [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md); revisión: [VALIDACION_F6_TOPOLOGIA_OUTBOX.md](VALIDACION_F6_TOPOLOGIA_OUTBOX.md). POST/outbox y consumidores siguen pendientes; entrega al menos una vez con deduplicación en F6-04.
- [ ] **F6-03** `POST /compras` pasa a **202 Accepted**: reserva + outbox; el cobro y la emisión ocurren en el consumidor.
  - Preparación: reserva, detalles, auditoría y un outbox `compra.procesar` por compra en la misma transacción. Payload `{ publicId }`, ID estable del outbox y sin datos personales. Contrato 202 + Location preparado y probado con disponibilidad sustituida solo en tests; producción conserva 201/200 mediante `CompraProcesamientoPendiente` (no existe flag para habilitarlo). 231 pruebas Release en verde, build sin warnings y format limpio. Activación y casilla pendientes de consumidor integrado con pagos/ledger F7-02/F7-03. Guía: [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).
- [ ] **F6-04** `ProcesarCompraConsumer`: idempotente (`mensajes_procesados`), ACK manual, reintentos con backoff, DLQ. Toma con `UPDATE compras SET estado='PROCESANDO', procesando_desde=now() WHERE id=@id AND estado='PENDIENTE'`; 0 filas ⇒ ACK y omitir. Referencia idempotente ante la pasarela: `compras.public_id`.
    - Componentes preparados: transporte v7, prefetch y despacho serial, ACK individual tras puerto/confirmación, retry persistente con TTL exponencial acotado, DLQ al tercer intento y reentrega si falla el traslado. Repositorios de toma condicional y deduplicación exigen transacción explícita; auditoría comparte la toma. Worker tiene host con reconexión, pero activar `Compras:ConsumerEnabled` falla cerrado sin `IProcesarCompraMensajePort` de F7 (no hay no-op ni cobro/emisión habilitados). 239 pruebas Release en verde, build sin warnings y format limpio. Integración de negocio y casilla pendientes; detalle en [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md) y revisión en [VALIDACION_F6_COMPRAS_CONSUMIDOR.md](VALIDACION_F6_COMPRAS_CONSUMIDOR.md).
- [x] **F6-05** Evento `compra.completada` → `EnviarConfirmacionConsumer` (correo + `notificaciones`).
  - Consumidor opt-in con scopes, prefetch=1, ACK tras commit, deduplicación transaccional y SMTP MailHog. Consulta compra COMPLETADA con respaldo financiero y emisión completa; destinatario/boletos desde BD, sin QR en logs ni notificaciones.mensaje. Tres intentos SMTP acotados; fallo terminal registra FALLIDO. 249 pruebas Release en verde, build sin warnings y format limpio. El productor de compra.completada sigue pendiente del finalizador F7; no cierra F6-03/F6-04 ni la salida de fase. Límite de duplicación SMTP ante caída entre envío/commit y comandos locales en [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md).
- [x] **F6-06** Flujo `usuario.registrado` → correo de bienvenida.
  - Registro existente → outbox → relay → RabbitMQ → MailHog → notificaciones, probado con transporte real. Consulta CLIENTE activo con perfil y correo actual desde PostgreSQL, ignorando email del evento. Deduplicación transaccional independiente, retry SMTP acotado y FALLIDO terminal; Worker ejecuta ambos consumidores con canales/scopes independientes. 256 pruebas Release en verde, build sin warnings y format limpio. Configuración y verificación desde Swagger en [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md); revisión del bloque en [VALIDACION_F6_NOTIFICACIONES.md](VALIDACION_F6_NOTIFICACIONES.md).
- [x] **F6-07** Job de expiración en `Worker` con bloqueo entre instancias.
  - Servicio opt-in, scope por ciclo, lote 100/intervalo 30 s configurables. Advisory lock transaccional PostgreSQL no bloqueante comparte estado/cupo/auditoría; otra instancia omite el ciclo, commit/rollback/dispose lo liberan. Conserva UPDATE condicional y solo PENDIENTE vencidas, sin tocar PROCESANDO. Dos hosts reales, rollback en shutdown y reintento con scope nuevo probados; 267 pruebas Release en verde, build sin warnings y format limpio. Configuración/Swagger local en [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md); revisión en [VALIDACION_F6_EXPIRACION_WORKER.md](VALIDACION_F6_EXPIRACION_WORKER.md).
- [ ] **F6-08** Pruebas con Testcontainers (RabbitMQ real): duplicado, fallo → DLQ, caída del consumidor.
- [ ] **F6-09** Prueba de carga básica (p. ej. 500–1000 compras concurrentes) y evidencia de resultados.
- [ ] **F6-10** Documentar topología con diagrama.
- [ ] **F6-11** Job de recuperación de compras PROCESANDO cuyo tiempo desde procesando_desde supera el timeout configurable (5 minutos por defecto): consultar pago por referencia compras.public_id; con pago APROBADO completar y emitir boletos idempotentemente; si no, pasar condicionalmente a FALLIDA y devolver cupo una sola vez en la misma transacción. Prueba de carrera procesar/expirar/cancelar ⇒ cupo devuelto exactamente una vez.

**Salida:** el flujo de compra corre por colas, resiste duplicados y fallos, recupera compras PROCESANDO atascadas sin duplicar cobros, boletos ni devoluciones de cupo, y queda evidencia en `notificaciones` y `outbox_mensajes`.

### Fase 7 — Finanzas

**Orden excepcional aprobado por el usuario:** después de F6-07, implementar F7-01 → F7-02 → F7-03, una tarea por commit, para proporcionar la base financiera que necesitan F6-03/F6-04 y sus pruebas completas. Después de F7-03, retomar la activación pendiente de F6-03/F6-04, F6-08 → F6-09 → F6-10 → F6-11 y cerrar los criterios de salida pendientes de F5/F6. Luego continuar F7-04 en adelante. No cerrar F6-08 con efectos artificiales de transporte ni habilitar COMPLETADA sin pago/ledger/saldos/emisión transaccionales. La consulta de la pasarela simulada debe conservar resultados ante reinicio antes de habilitar recuperación F6-11. Las aprobaciones de dinero para este bloque están confirmadas; cualquier tabla/columna/endpoint que no esté definido sigue requiriendo consulta al usuario.

- [x] **F7-01** Cuentas: alta automática de cuenta `DUENO` al crear dueño; cuentas `PLATAFORMA` e `IMPUESTOS` únicas por moneda. Cierre lógico solo de DUENO con saldo cero, estado CERRADA y fecha_cierre; PLATAFORMA/IMPUESTOS no se cierran; trigger rechaza movimientos en CERRADA.
  - Contrato aprobado por el usuario: POST /api/v1/cuentas/{id}/cerrar sin body, dueño sobre su cuenta o administrador habilitado; 200 al cerrar/repetir, 404 ajena/inexistente y 409 con saldo o cuenta PLATAFORMA/IMPUESTOS. Alta transaccional mediante puerto de Finance desde Identity y cierre con propiedad, UPDATE condicional y auditoría; reintentos conservan la fecha persistida. Diseño y comandos locales en [CUENTAS_FINANCIERAS.md](CUENTAS_FINANCIERAS.md).
  - Verificación: 287 pruebas Release aprobadas, cero fallos/omitidas, build sin warnings y formato limpio; incluye veinte cierres concurrentes, rollback de auditoría, trigger de cuentas cerradas y contrato HTTP/Swagger. Sin cambios de esquema ni paquetes nuevos.
- [x] **F7-02** Registro de `pagos` por intento y su estado.
  - Puerto interno transaccional: intento PENDIENTE con total congelado/public_id; resultado APROBADO/RECHAZADO validado en Domain, transición condicional, auditoría e idempotencia por ID de intento. Bloqueo de compra serializa concurrencia sin cambiar esquema. 304 pruebas Release aprobadas, build sin warnings y formato limpio. Guía y comandos: [REGISTRO_PAGOS.md](REGISTRO_PAGOS.md). Integración con ledger en F7-03; consulta durable de la pasarela simulada aún pendiente antes de F6-11.
- [x] **F7-03** Movimientos al completar compra (`VENTA` +subtotal al dueño, `COMISION` +comision_plataforma a PLATAFORMA, `IMPUESTO` +impuestos a IMPUESTOS), saldos y compra en la misma transacción; movimientos de cobro suman compras.total antes de reembolsos. Componentes cero no generan movimientos (CHECK monto ≠ 0).
  - Finalizador interno en Application: pago APROBADO, ledger, saldos, transición PROCESANDO → COMPLETADA, emisión, auditorías y outbox juntos; sin recalcular tasas ni publicar dentro de la transacción. Reintentos exigen respaldo íntegro. 325 pruebas Release aprobadas, build sin warnings y formato limpio: veinte finalizaciones duplicadas, diez compras con cuentas compartidas y rollback en cada etapa. Guía: [FINALIZACION_FINANCIERA.md](FINALIZACION_FINANCIERA.md). Retomar activación F6-03/F6-04 después del PR de base financiera; consulta durable de la pasarela pendiente antes de F6-11. F7-04 a F7-08 siguen pendientes.
- [ ] **F7-04** Reembolso: revierte los tres componentes con REEMBOLSO en las mismas cuentas; anula condicionalmente boletos VALIDO → ANULADO y devuelve solo ese número de cupos, una sola vez en la misma transacción.
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
| IVA y alcance fiscal | Tasa configurable en appsettings y cálculo en Domain; conciliación de los tres componentes del cobro. Facturación electrónica fuera de alcance |
| Reembolso parcial | Fuera del alcance actual; solo reembolso total que revierte movimientos registrados, sin recalcular tasas |
| Compras atascadas o carreras de estado | F6-11: timeout configurable, consulta de pago por public_id y recuperación idempotente; toda transición usa estado esperado y solo el ganador devuelve cupo |
