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
| D-05 | LLM y embeddings | ✅ | **Ollama Cloud** `gpt-oss:120b` para chat; Gemini API `gemini-embedding-2` para embeddings de 1536 dimensiones en nivel gratuito, sin modelos locales. `.env` ignorado con OLLAMA_API_KEY y GEMINI_API_KEY, gestionadas por el usuario; C#/HttpClient y pruebas sin consumo cloud |
| D-06 | Migraciones | ✅ | SQL con Flyway; EF Core no genera migraciones |
| D-07 | ¿Refresh tokens además del JWT? | ✅ | Sí; tabla `refresh_tokens`, rotación y revocación de familia ante reutilización |
| D-08 | ¿Asientos numerados? | ✅ | Venta mixta general/numerada por evento; mesas completas o por silla, QR individual y precio por silla. Diseño EXT-NUM aprobado; implementación pendiente |
| D-09 | Tecnología del frontend | ⬜ pendiente hasta Fase 11 | Opciones: Blazor (todo en .NET), React o Angular |
| D-10 | Pasarela de pago | ✅ | Adaptador simulado detrás de `IPaymentGateway` |
| D-11 | Quién asume IVA y comisión | ✅ | Comisión como cargo al cliente; IVA aparte, tasa configurable y cálculo en Domain; cuenta IMPUESTOS (BASE_DE_DATOS.md §4.5) |
| D-12 | Roles | ✅ | Un rol por usuario (`usuarios.rol` + perfil) |
| D-13 | Una compra = un evento | ✅ | `compras.evento_id` |
| D-14 | Envío de correo | ✅ | MailHog en desarrollo |
| D-15 | Política monetaria de compra | ✅ | Precios sin IVA; `Finanzas:TasaIva = 0.12`, `Finanzas:ComisionPorcentaje = 0.10` (valor inicial configurable, no dato legal). Comisión sobre subtotal, IVA sobre subtotal + comisión; decimal y redondeo AwayFromZero por componente a 2 decimales; montos congelados y reembolso solo total |

Las decisiones D-07, D-08, D-11 y D-14 están confirmadas. D-09 se resolverá en F11-01. F0-01 está cerrada con este aplazamiento explícito; F0-02 permanece pendiente de confirmación del usuario, quien gestiona OLLAMA_API_KEY en .env local ignorado o configuración externa.

Acuerdo F6-11 aprobado por el usuario: V11 añade `pagos_simulados` (referencia UUID PK, monto NUMERIC(10,2), moneda VARCHAR(3), estado APROBADO/RECHAZADO y fecha TIMESTAMPTZ; todos obligatorios). Diario inmutable sin FK, commit independiente del proveedor simulado. No modifica el ledger ni añade endpoints. API/Worker usan la consulta durable; la recuperación nunca cobra y no devuelve cupo ante error de consulta o respaldo histórico inconsistente.

El modelo implementado tiene **31 tablas**, incluidas `refresh_tokens` y `pagos_simulados` (26 originales, V11 aprobada en F6-11 y cuatro tablas de V12/EXT-NUM-02). Las migraciones son la fuente de verdad del esquema implementado. Para tipos, claves o nulabilidad omitidos de tablas existentes se usa [`referencia/schema_v1.sql`](referencia/schema_v1.sql) con las correcciones v2; nunca se ejecuta ese script ni se mueve a migraciones. Para tablas nuevas se sigue BASE_DE_DATOS.md y se registra cada suposición en el encabezado de la migración. F1-11 regeneró el diccionario completo desde V1–V9, ampliado con V11.

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
| Q6 | RabbitMQ en un flujo de negocio real y recuperación de compras atascadas | F6-01 … F6-11 (incluye recuperación y prueba de carrera) | ✅ |
| Q7 | Agente de IA con información pública | F9-06, F9-07 | ✅ |
| Q8 | RAG: ingesta, recuperación y citación | F9-01 … F9-05 | ✅ |
| Q9 | Logs y auditoría (sistema + agente IA) | F3-06, F9-08, F1-07 | ✅ |
| Q10 | ≥ 15 reportes con filtros y totales, exportables a Excel y PDF | F10-01 … F10-05 | ⬜ |
| Q11 | Integración con Power BI | F1-08, F10-06 | ⬜ |
| Q12 | Frontend funcional e integrado | F11-* | ⬜ |
| Q13 | Entregables: CLAUDE.md, AGENTS.md y Manual Técnico | F0-05, F12-04 | ⬜ |
| Q14 | Funcional: venta por localidad/general, alta demanda, finanzas, mapas, reseñas, 3 perfiles | F4-*, F5-*, F6-*, F7-*, F8-* | ⬜ |

---

## 2. Fases y tareas

### Fase 0 — Decisiones y arranque
- [x] **F0-01** Cerrar D-07, D-08, D-11 y D-14; aplazar explícitamente D-09 hasta F11-01.
- [ ] **F0-02** Crear cuentas/claves Ollama Cloud y Gemini API Free Tier; guardarlas en `.env` local ignorado o configuración externa, sin facturación ni compra de créditos. Verificar acceso gratuito de la cuenta al modelo de chat. Pendiente de confirmación del usuario; sustituye la selección histórica de OpenAI según D-05.
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

**Salida:** BD completa (26 tablas al cerrar Fase 1; 27 desde V11/F6-11) creada solo con migraciones; vistas `bi` consultables y diccionario completo regenerado desde las migraciones.

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
  - Histórico F5-03 (actualizado por el contrato 202 de F6-03): HTTP 201 nueva / 200 repetida propia; compra PENDIENTE, reserva + detalles + auditoría transaccionales. D-15 congelada, validación del actor y padres activos, límite agregado y carreras de clave. 159 pruebas Release en verde; 54 operaciones Swagger. Guía frontend/local en [COMPRAS_API.md](COMPRAS_API.md) y revisión del bloque en [VALIDACION_F5_COMPRAS.md](VALIDACION_F5_COMPRAS.md). Componentes de pago/emisión F5-04/F5-05 implementados; dependencia financiera resuelta por F7-02/F7-03 y flujo integrado en F6-03/F6-04; handler de expiración en F5-08, programación automática en F6-07 y contrato 202/outbox en F6-03.
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
- [x] **F5-09** **Prueba de concurrencia:** N hilos sobre cupo K ⇒ vendidos = K exactos.
  - Reserva: ocho clientes HTTP, casos 40/7, 40/8 y 33/9 con cantidades 1/2/3. Ventas completas: F6-09 aporta 500/1000 solicitudes concurrentes contra cupo 100, exactamente 100 compras COMPLETADA, pagos APROBADO, boletos y ledger/saldos conciliados en cada escenario. Verificadas nuevamente con el proveedor durable V11 en las 360 pruebas Release aprobadas de F6-11. Evidencia: [PRUEBAS_F5_CONCURRENCIA.md](PRUEBAS_F5_CONCURRENCIA.md) y [PRUEBAS_F6_CARGA.md](PRUEBAS_F6_CARGA.md); revisión de salida: [VALIDACION_FASES_5_6.md](VALIDACION_FASES_5_6.md).
- [x] **F5-10** Prueba de idempotencia: misma `Idempotency-Key` ⇒ una sola compra.
  - Cuarenta solicitudes HTTP con una clave propia, disputa entre clientes, reintento tras falta de cupo y reutilización tras cancelar/cambiar precio: una compra/detalle/auditoría y una sola reserva. Evidencia: [PRUEBAS_F5_IDEMPOTENCIA.md](PRUEBAS_F5_IDEMPOTENCIA.md). Consumidores en F6 y finalización/ledger/emisión se validarán al integrar F7; salida de Fase 5 y F5-09 pendientes.

**Salida:** compra completa y correcta de punta a punta, sin sobreventa bajo concurrencia.

Salida Fase 5 cerrada localmente con F5-09/F6-11 y la integración financiera adelantada F7-01–F7-03. El contrato vigente es 202 + Location y procesamiento por Worker; no se reintroduce finalización síncrona. Revisión y límites en [VALIDACION_FASES_5_6.md](VALIDACION_FASES_5_6.md). CI remoto se verifica al publicar el PR.

**Dependencia resuelta con autorización del usuario:** F5-04/F5-05 se prepararon como componentes y F7-01–F7-03 se adelantaron para exigir ledger en la misma transacción de COMPLETADA. F6-03/F6-04 integran el contrato 202 y el Worker; F6-11 añade el diario durable. F5-09 cierra la evidencia de ventas completas. No se marcaron tareas financieras antes de implementarlas. Un error de consulta o respaldo histórico inconsistente no autoriza devolver cupo.

### Fase 6 — Venta asíncrona con RabbitMQ
- [x] **F6-01** Abstracciones `IMessagePublisher` y topología (exchanges, colas, retry, DLQ) declarada en código.
  - RabbitMQ.Client 7.2.2 async v7 bajo Apache 2.0, configuración externa, canal serializado, persistencia y confirms con mandatory. Cinco pruebas RabbitMQ real; 221 pruebas Release en verde, build sin warnings y format limpio. Retry con TTL por mensaje para backoff en F6-04. Guía: [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). Sin consumidores ni cambios HTTP todavía.
- [x] **F6-02** Outbox: escritura transaccional y **relay** en `Worker` (`SKIP LOCKED`, confirmaciones).
  - Writer scoped exige transacción de negocio; relay independiente por mensaje, UPDATE condicional tras confirms, intentos persistidos y reintento con ID estable. Worker periódico configurable, opt-in y shutdown async. Siete pruebas PostgreSQL+RabbitMQ (rollback, concurrencia, fallo/retorno, caída antes de commit y host real); 228 pruebas Release en verde, build sin warnings y format limpio. Guía y comandos separados: [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md); revisión: [VALIDACION_F6_TOPOLOGIA_OUTBOX.md](VALIDACION_F6_TOPOLOGIA_OUTBOX.md). POST/outbox y consumidores siguen pendientes; entrega al menos una vez con deduplicación en F6-04.
- [x] **F6-03** `POST /compras` pasa a **202 Accepted**: reserva + outbox; el cobro y la emisión ocurren en el consumidor.
  - Contrato 202 + Location activo con el módulo real: reserva/detalles/auditoría/outbox transaccionales, consumidor financiero F6-04 y polling propio hasta estado final. Misma clave responde 202 mientras está PENDIENTE/PROCESANDO y 200 en estado final. Recorrido HTTP→RabbitMQ→pago/ledger/boletos→polling COMPLETADA probado con el procesador real; 335 pruebas Release aprobadas, build sin warnings y formato limpio. Asistentes por ítem aprobados, obligatorios en carreras; guardados en outbox, eliminados del mensaje RabbitMQ. Guía: [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).
- [x] **F6-04** `ProcesarCompraConsumer`: idempotente (`mensajes_procesados`), ACK manual, reintentos con backoff, DLQ. Toma con `UPDATE compras SET estado='PROCESANDO', procesando_desde=now() WHERE id=@id AND estado='PENDIENTE'`; 0 filas ⇒ ACK y omitir. Referencia idempotente ante la pasarela: `compras.public_id`.
    - Corrección de sincronización de pruebas durante el PR de Fase 8: esperar deduplicación y efecto confirmados en PostgreSQL antes de verificar el reintento exitoso; comprobar también la cola retry. El escenario pausa el segundo intento antes del commit y demuestra que dos invocaciones no garantizan un efecto persistido. Sin cambios en el consumidor de producción ni en el esquema.
    - Integración completada: procesador real con pago simulado, ledger/saldos, emisión, auditoría y outbox en la misma transacción que la toma/deduplicación; rechazo devuelve cupo una vez. Asistentes por ítem aprobados, guardados en outbox; relay envía solo publicId. Consulta durable y recuperación pendientes de F6-11. 334 pruebas Release aprobadas, build sin warnings y formato limpio. La activación HTTP F6-03 se cierra en el commit siguiente de este bloque. Guía actualizada: [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).
    - Preparación anterior validada: transporte v7, ACK tras commit, retry/DLQ, toma condicional y repositorio de deduplicación. Ahora AddProcesamientoCompras registra el integrador real; la activación requiere conexión, RabbitMQ, clave QR y categoría de carreras externos. Las pruebas aisladas de transporte no sustituyen las pruebas con compras pagadas.
- [x] **F6-05** Evento `compra.completada` → `EnviarConfirmacionConsumer` (correo + `notificaciones`).
  - Consumidor opt-in con scopes, prefetch=1, ACK tras commit, deduplicación transaccional y SMTP MailHog. Consulta compra COMPLETADA con respaldo financiero y emisión completa; destinatario/boletos desde BD, sin QR en logs ni notificaciones.mensaje. Tres intentos SMTP acotados; fallo terminal registra FALLIDO. 249 pruebas Release en verde, build sin warnings y format limpio. El productor de compra.completada se incorporó en F7-03 y ahora lo invoca el consumidor real F6-04; la salida de fase sigue pendiente de las tareas restantes. Límite de duplicación SMTP ante caída entre envío/commit y comandos locales en [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md).
- [x] **F6-06** Flujo `usuario.registrado` → correo de bienvenida.
  - Registro existente → outbox → relay → RabbitMQ → MailHog → notificaciones, probado con transporte real. Consulta CLIENTE activo con perfil y correo actual desde PostgreSQL, ignorando email del evento. Deduplicación transaccional independiente, retry SMTP acotado y FALLIDO terminal; Worker ejecuta ambos consumidores con canales/scopes independientes. 256 pruebas Release en verde, build sin warnings y format limpio. Configuración y verificación desde Swagger en [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md); revisión del bloque en [VALIDACION_F6_NOTIFICACIONES.md](VALIDACION_F6_NOTIFICACIONES.md).
- [x] **F6-07** Job de expiración en `Worker` con bloqueo entre instancias.
  - Servicio opt-in, scope por ciclo, lote 100/intervalo 30 s configurables. Advisory lock transaccional PostgreSQL no bloqueante comparte estado/cupo/auditoría; otra instancia omite el ciclo, commit/rollback/dispose lo liberan. Conserva UPDATE condicional y solo PENDIENTE vencidas, sin tocar PROCESANDO. Dos hosts reales, rollback en shutdown y reintento con scope nuevo probados; 267 pruebas Release en verde, build sin warnings y format limpio. Configuración/Swagger local en [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md); revisión en [VALIDACION_F6_EXPIRACION_WORKER.md](VALIDACION_F6_EXPIRACION_WORKER.md).
- [x] **F6-08** Pruebas con Testcontainers (RabbitMQ real): duplicado, fallo → DLQ, caída del consumidor. Cinco escenarios con el procesador real; guía y evidencia en `docs/PRUEBAS_F6_RESILIENCIA.md`. Suite completa: 340 pruebas aprobadas.
- [x] **F6-09** Prueba de carga básica (p. ej. 500–1000 compras concurrentes) y evidencia de resultados. API real con 500/1000 solicitudes simultáneas contra cupo 100; 100 compras COMPLETADA, pagos, boletos y ledger conciliado en cada escenario. Tiempos y comandos en [PRUEBAS_F6_CARGA.md](PRUEBAS_F6_CARGA.md). Suite Release: 342 aprobadas, ninguna fallida/omitida; build sin warnings y formato limpio.
- [x] **F6-10** Documentar topología con diagrama. [TOPOLOGIA_RABBITMQ.md](TOPOLOGIA_RABBITMQ.md): exchanges/rutas/colas, TTL/backoff/DLQ, confirms, ACK, deduplicación y límites del pago simulado. Guías de outbox/correo actualizadas al flujo financiero activo; ampliadas con F6-11 y su diario durable.
- [x] **F6-11** Job de recuperación de compras PROCESANDO cuyo tiempo desde procesando_desde supera el timeout configurable (5 minutos por defecto): consultar pago por referencia compras.public_id; con pago APROBADO completar y emitir boletos idempotentemente; rechazo o ausencia confirmada sin efectos financieros pasa condicionalmente a FALLIDA y devuelve cupo una sola vez. Consulta fallida o respaldo histórico inconsistente conserva PROCESANDO para conciliación. Diario durable aprobado V11, job opt-in, rollback de deduplicación provisional y seis carreras procesar/expirar/cancelar/recuperar. Dieciocho casos nuevos; 360 pruebas Release aprobadas, 0 fallidas/omitidas; build sin warnings, formato limpio. Guía y límites: [RECUPERACION_COMPRAS.md](RECUPERACION_COMPRAS.md).

**Salida:** el flujo de compra corre por colas, resiste duplicados y fallos, recupera compras PROCESANDO atascadas sin duplicar cobros, boletos ni devoluciones de cupo, y queda evidencia en `notificaciones` y `outbox_mensajes`.

Salida Fase 6 cerrada localmente en el alcance de la pasarela simulada: F6-01–F6-11 completas, diario V11 aprobado, 360 pruebas Release en verde, build sin warnings y formato limpio. Revisión de §§5/12 y límites en [VALIDACION_FASES_5_6.md](VALIDACION_FASES_5_6.md). No hay integración de cobro externo ni conciliación administrativa automática; CI remoto se verifica al publicar el PR.

### Fase 7 — Finanzas

**Orden excepcional aprobado por el usuario:** después de F6-07, implementar F7-01 → F7-02 → F7-03, una tarea por commit, para proporcionar la base financiera que necesitan F6-03/F6-04 y sus pruebas completas. Después de F7-03, retomar la activación pendiente de F6-03/F6-04, F6-08 → F6-09 → F6-10 → F6-11 y cerrar los criterios de salida pendientes de F5/F6. Luego continuar F7-04 en adelante. No cerrar F6-08 con efectos artificiales de transporte ni habilitar COMPLETADA sin pago/ledger/saldos/emisión transaccionales. La consulta de la pasarela simulada debe conservar resultados ante reinicio antes de habilitar recuperación F6-11. Las aprobaciones de dinero para este bloque están confirmadas; cualquier tabla/columna/endpoint que no esté definido sigue requiriendo consulta al usuario.

- [x] **F7-01** Cuentas: alta automática de cuenta `DUENO` al crear dueño; cuentas `PLATAFORMA` e `IMPUESTOS` únicas por moneda. Cierre lógico solo de DUENO con saldo cero, estado CERRADA y fecha_cierre; PLATAFORMA/IMPUESTOS no se cierran; trigger rechaza movimientos en CERRADA.
  - Contrato aprobado por el usuario: POST /api/v1/cuentas/{id}/cerrar sin body, dueño sobre su cuenta o administrador habilitado; 200 al cerrar/repetir, 404 ajena/inexistente y 409 con saldo o cuenta PLATAFORMA/IMPUESTOS. Alta transaccional mediante puerto de Finance desde Identity y cierre con propiedad, UPDATE condicional y auditoría; reintentos conservan la fecha persistida. Diseño y comandos locales en [CUENTAS_FINANCIERAS.md](CUENTAS_FINANCIERAS.md).
  - Verificación: 287 pruebas Release aprobadas, cero fallos/omitidas, build sin warnings y formato limpio; incluye veinte cierres concurrentes, rollback de auditoría, trigger de cuentas cerradas y contrato HTTP/Swagger. Sin cambios de esquema ni paquetes nuevos.
- [x] **F7-02** Registro de `pagos` por intento y su estado.
  - Puerto interno transaccional: intento PENDIENTE con total congelado/public_id; resultado APROBADO/RECHAZADO validado en Domain, transición condicional, auditoría e idempotencia por ID de intento. Bloqueo de compra serializa concurrencia sin cambiar esquema. Verificación histórica F7-02: 304 pruebas Release aprobadas, build sin warnings y formato limpio. Guía y comandos: [REGISTRO_PAGOS.md](REGISTRO_PAGOS.md). Integración con ledger completada en F7-03 y consulta durable de la pasarela simulada en F6-11.
- [x] **F7-03** Movimientos al completar compra (`VENTA` +subtotal al dueño, `COMISION` +comision_plataforma a PLATAFORMA, `IMPUESTO` +impuestos a IMPUESTOS), saldos y compra en la misma transacción; movimientos de cobro suman compras.total antes de reembolsos. Componentes cero no generan movimientos (CHECK monto ≠ 0).
  - Finalizador interno en Application: pago APROBADO, ledger, saldos, transición PROCESANDO → COMPLETADA, emisión, auditorías y outbox juntos; sin recalcular tasas ni publicar dentro de la transacción. Reintentos exigen respaldo íntegro. Verificación histórica F7-03: 325 pruebas Release aprobadas, build sin warnings y formato limpio: veinte finalizaciones duplicadas, diez compras con cuentas compartidas y rollback en cada etapa. Guía: [FINALIZACION_FINANCIERA.md](FINALIZACION_FINANCIERA.md). Activación F6-03/F6-04 completada en el bloque de integración y consulta durable de la pasarela en F6-11.
- [x] **F7-04** Reembolso: revierte los tres componentes con REEMBOLSO en las mismas cuentas; anula condicionalmente boletos VALIDO → ANULADO y devuelve solo ese número de cupos, una sola vez en la misma transacción.
  - Contrato y reglas aprobados por el usuario: POST /api/v1/compras/{publicId}/reembolsar sin body; solo ADMINISTRADOR habilitado. 200 también al repetir, 404 inexistente, 409 estado/respaldo/cuenta cerrada/saldo insuficiente. Reembolso total aun con USADO, que conserva historial y no devuelve cupo; compra permanece COMPLETADA y pago pasa a REEMBOLSADO. No cambia esquema ni realiza transferencia externa. Guía: [REEMBOLSOS.md](REEMBOLSOS.md).
  - Verificación histórica F7-04: 387 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. 27 casos nuevos: ocho de dominio, trece de PostgreSQL y seis de API. Veinte administradores, compras con cuentas compartidas, rollback en cuatro etapas y tres carreras HTTP contra escaneo. Swagger: 61 operaciones en ese bloque. Arranque de fixtures acotado por ensamblado para evitar agotamiento de redes Docker, conservando concurrencia dentro de las pruebas.
- [x] **F7-05** Liquidación a dueños (`LIQUIDACION`) con validación de saldo.
  - Contrato aprobado: POST /api/v1/cuentas/{id}/liquidar, solo ADMINISTRADOR habilitado, body monto e Idempotency-Key obligatorio por cuenta. DUENO ACTIVA con saldo suficiente y conciliado; débito, saldo y auditoría en la misma transacción. Referencia SHA-256 en descripcion bajo bloqueo de cuenta sin cambio de esquema. Repetir mismo monto devuelve 200 original incluso tras cierre; otro monto con la misma clave devuelve 409. Alcance contable simulado, sin transferencia bancaria. Guía: [LIQUIDACIONES.md](LIQUIDACIONES.md).
  - Verificación histórica F7-05: 409 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. 22 casos nuevos (11 Domain, 8 PostgreSQL, 3 API), veinte administradores distintos, competencia por saldo, rollback, conciliación y carrera contra reembolso. Respuesta inicial usa la fecha persistida para igualdad exacta en reintentos. Swagger: 62 operaciones en ese bloque.
- [x] **F7-06** Consultas: estado de cuenta por dueño, extracto de movimientos con filtros.
  - Tres rutas GET aprobadas: /api/v1/cuentas, /api/v1/cuentas/{id}, /api/v1/cuentas/{id}/movimientos. ADMINISTRADOR todas; DUENO_RECINTO solo propias, incluidas cerradas; cuenta ajena/inexistente 404 y CLIENTE 403. Filtros por dueño/moneda/tipo/estado y extracto por tipo/compra/intervalo UTC desde inclusivo/hasta exclusivo. Paginación default 20/máximo 100, fechas e IDs del extracto descendentes. Sin esquema ni paquetes nuevos. Guía: [CONSULTAS_FINANCIERAS.md](CONSULTAS_FINANCIERAS.md).
  - Verificación: 414 pruebas Release aprobadas, cero fallidas/omitidas, build sin warnings y formato limpio. Cinco casos API nuevos con PostgreSQL real: permisos, propiedad, filtros, páginas, límites exactos de fechas, token anterior a baja/cambio obligatorio y Swagger (65 operaciones).
- [x] **F7-07** Prueba de conciliación: `saldo == SUM(movimientos)` tras escenarios mixtos.
  - Dos escenarios nuevos con adaptadores reales y PostgreSQL: ventas pagadas/gratuita, reembolso, liquidaciones parciales del saldo, reintentos, cierre y consulta; seis administradores concurrentes mezclan tres reembolsos y tres retiros completos. Saldo/SUM por cuenta se comparan en un único snapshot tras cada etapa y al terminar la concurrencia. No se inventan efectos financieros. Evidencia: [VALIDACION_FASE_7.md](VALIDACION_FASE_7.md).
  - Verificación histórica F7-07: 416 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio.
- [x] **F7-08** Verificar que `UPDATE`/`DELETE` sobre el ledger falla (permisos/trigger).
  - Cuatro casos apuntan a movimientos de ventas reales en PostgreSQL temporal: test_app/promotora_app rechaza UPDATE/DELETE por permisos (42501); flyway propietario, aun con privilegios, por trigger (23514). Transacciones revertidas y fila, saldo/SUM y cantidad de movimientos intactos. Sin cambios de permisos, triggers ni migraciones. Evidencia: [VALIDACION_FASE_7.md](VALIDACION_FASE_7.md).
  - Verificación final: 420 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. Once casos nuevos en F7-06–F7-08, Swagger con 65 operaciones.

**Salida:** el dinero cuadra siempre y cada centavo es trazable.

Salida Fase 7 cerrada localmente en el alcance simulado: F7-01–F7-08 completas. Revisión del diff frente al criterio de salida y AGENTS.md §§5/12 sin incumplimientos detectados; evidencia y límites en [VALIDACION_FASE_7.md](VALIDACION_FASE_7.md). Sin transferencia bancaria/pasarela externa; CI remoto se verifica al publicar el PR.

### Fase 8 — Reseñas
- [x] **F8-01** Crear reseña: solo con boleto `USADO` del evento, una por (evento, cliente).
  - Contrato aprobado: POST /api/v1/resenas, CLIENTE habilitado con boleto USADO, nace PENDIENTE; calificación 1–5 y comentario opcional máximo 510 caracteres. Cliente desde sesión, unicidad PostgreSQL con ON CONFLICT y auditoría transaccional. Sin cambios de esquema/paquetes. Guía: [RESENAS.md](RESENAS.md).
  - Verificación: 428 pruebas Release aprobadas, cero fallidas/omitidas, build sin warnings y formato limpio. Seis casos Domain y dos API nuevos (510/511, roles, asistencia real, evento ajeno y diez creaciones concurrentes). Swagger: 66 operaciones. F8-02–F8-05 aprobadas para continuar en orden; detener antes de Fase 9 por indicación del usuario.
- [x] **F8-02** Moderación por administrador (`APROBADA`/`RECHAZADA`).
  - GET /api/v1/resenas: cola administrativa paginada, PENDIENTE por defecto, filtros estado/eventoId. PATCH /api/v1/resenas/{id}/moderar con estadoNuevo APROBADA/RECHAZADA, solo ADMINISTRADOR habilitado, desde PENDIENTE; misma decisión repetida 200 sin otra auditoría, incompatible 409. Bloqueo de fila, UPDATE condicional y MODERATE transaccional. Guía: [RESENAS.md](RESENAS.md).
  - Verificación: 436 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. Seis casos Domain y dos API nuevos, incluida carrera de dos administradores distintos. Swagger: 68 operaciones.
- [x] **F8-03** Listado público de reseñas aprobadas por evento.
  - GET /api/v1/eventos/{id}/resenas, anónimo, solo APROBADA; página 1–100000 y tamaño 1–100 (default 20), orden fecha/ID descendentes. Sin identidad del cliente; evento inexistente 404, existente sin aprobadas 200 vacío. Guía: [RESENAS.md](RESENAS.md).
  - Verificación: 437 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. Nuevo caso API de privacidad, estados, evento y paginación. Swagger: 69 operaciones.
- [x] **F8-04** Agregados por presentador (promedio, cantidad) vía `evento_presentadores`.
  - GET /api/v1/presentadores/{id}/calificacion, público, solo APROBADA mediante evento_presentadores. Agregado suma/cantidad en un snapshot; media por reseña a dos decimales en Domain. Sin aprobadas: promedio null/cantidad 0; inexistente/inactivo 404. Sin datos privados. Guía: [RESENAS.md](RESENAS.md).
  - Verificación: 442 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. Cuatro casos Domain y uno API con asociaciones/co-presentadores y exclusión de pendientes/rechazadas. Swagger: 70 operaciones.
- [x] **F8-05** Editar/retirar reseña propia; auditoría.
  - GET /api/v1/resenas/mis, PUT /api/v1/resenas/{id} y POST /api/v1/resenas/{id}/retirar solo CLIENTE habilitado/autor. Estado esperado explícito, bloqueo y UPDATE condicional; edición retorna PENDIENTE, retiro RECHAZADA sin borrar/liberar unicidad; reenvío por edición. Auditoría UPDATE con motivos ResenaEditada/ResenaRetirada y MODERATE con motivos de decisión, sin comentario. Guía: [RESENAS.md](RESENAS.md).
  - Verificación final: 454 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. Doce casos nuevos en esta tarea: seis Domain y seis API, propiedad, reintentos, retiro/reenvío, cambios de estado concurrentes, pérdida de publicación/promedio hasta moderar, tokens anteriores a baja y rollback de edición/retiro. Swagger: 73 operaciones.

**Salida:** reseñas moderadas y calificación por artista/equipo visible.

Salida Fase 8 cerrada localmente: F8-01–F8-05 completas, 454 pruebas Release aprobadas (34 casos nuevos), build sin warnings y formato limpio. Revisión del diff y AGENTS.md §§5/12 sin incumplimientos detectados; evidencia y límites en [VALIDACION_FASE_8.md](VALIDACION_FASE_8.md). CI remoto se verifica al publicar el PR. La pausa antes de Fase 9 queda resuelta por la elección de Ollama según D-05.

### Fase 9 — Agente de IA y RAG (Ollama)
- [x] **F9-01** Puertos `ILlmPort`, `IEmbeddingPort`, `IVectorSearchPort` y adaptadores Ollama Cloud (chat), Gemini API Free Tier (embeddings) + pgvector. Conservar vector(1536), sin rellenar vectores artificialmente. Configuración `.env` local solo en desarrollo; límites de entrada/salida y pruebas HTTP simuladas, sin llamadas reales.
  - Configuración local adicional autorizada el 13/10/2026: .env privado con credenciales existentes y claves entregadas por el usuario; Start-Local.ps1 carga la configuración completa para API/Worker y Compose admite RABBITMQ_USER. Prueba real acotada: Gemini 1536 dimensiones y una conversación Ollama (371 entrada/63 salida), sin citación RAG todavía. Docker/Flyway, autenticación RabbitMQ y salud/Swagger verificados; acceso del broker limitado a recursos de la aplicación. Los secretos nunca se versionan. En ese cierre seguían pendientes F9-02 y siguientes.
  - D-05 actualizada en commit de documentación previo. Adaptadores C#/HttpClient, prompt fijo de José con datos delimitados, límites de tamaño/tiempo y rechazo de salida incompleta; claves separadas a hosts HTTPS fijos de Ollama/Google, sin redirecciones ni reintentos automáticos. Embeddings cloud en lotes de hasta 16, con una solicitud estándar por texto y búsqueda por coseno solo sobre INDEXADO. Activación opt-in, .env acotado a OLLAMA_API_KEY y GEMINI_API_KEY en Development, configuración externa tiene prioridad. Verificación cloud final: 484 pruebas Release aprobadas, 0 fallidas/omitidas (30 casos nuevos netos desde Fase 8); build sin warnings y format limpio. Microsoft.Extensions.Http 10.0.10, MIT, para gestión de conexiones; sin migraciones ni endpoints nuevos. Guía: [IA_OLLAMA.md](IA_OLLAMA.md). Alcance de ese cierre limitado a F9-01; no se declara completa la defensa contra prompt injection ni habilitado el chat público.
- [x] **F9-02** Ingesta: `POST /ia/documentos` (PDF/DOCX/TXT/MD) con extracción de texto; CRUD y versionado de `documentos_rag`.
  - Cinco operaciones administrativas: carga, listado paginado, detalle, nueva versión y desactivación. Texto en PostgreSQL, referencia lógica privada de serie en ruta_archivo; bloqueo por serie, estados condicionales y auditoría transaccional. Sin cambios de esquema ni llamadas cloud; documentos PENDIENTE hasta F9-04. PdfPig 0.1.16 (Apache-2.0) y DocumentFormat.OpenXml 3.5.1 (MIT), solo Infrastructure. Build sin warnings y format limpio; 507 casos verificados por ensamblados (357 en los otros cuatro + 150 de API), con 23 nuevos. El primer pase global detectó el inventario Swagger antiguo de 73; actualizado a 78 y repetido todo API en verde. Guía, contratos y límites: [IA_DOCUMENTOS.md](IA_DOCUMENTOS.md). En aquel cierre F9-02 seguían pendientes F9-03–F9-11; ver el cierre F9-11 para el estado vigente.
- [x] **F9-03** Chunking con solapamiento y preparación de página/orden; su persistencia ocurre en F9-04 junto con el embedding obligatorio.
  - Fragmentación Unicode, ~3200 unidades por fragmento (~800 tokens aproximados), solapamiento de un octavo (~100 tokens), máximo 5500 bytes UTF-8 y 2000 fragmentos. Sin tokenizer exacto del proveedor. PDF conserva página física, otros formatos página null. Tres pruebas focalizadas aprobadas; build Application sin warnings. No se insertan vectores ficticios.
- [x] **F9-04** Embeddings en lote → `chunks_rag`; estados `PENDIENTE → INDEXADO/ERROR`; reindexación.
  - Ruta aprobada POST /api/v1/ia/documentos/{id}/reindexar, ADMINISTRADOR habilitado. Indexación explícita para controlar consumo: máximo 32 fragmentos, lotes de 16. Bloqueo PostgreSQL entre instancias, cloud fuera de transacciones, publicación y auditoría atómicas. Nuevas versiones publicadas retiran las anteriores INDEXADO; error conserva un índice ya publicado. Modelo en metadata; control de versiones/estado al publicar. Prueba focalizada de PostgreSQL aprobada (bloqueo, rollback y reemplazo), build sin warnings.
- [x] **F9-05** Recuperación top-k con umbral y filtros; respuesta con **citas** (documento, página, chunk).
  - topK 3/umbral 0.65, contexto acotado y filtro del modelo en metadata. Citas devueltas con documento, página, chunk, ID y extracto público. Generación extractiva: el modelo selecciona citas literales y el servidor rechaza fuentes desconocidas o texto inventado; no publica prosa libre sin respaldo. Pruebas focalizadas de citas y pgvector aprobadas, compilación limpia.
- [x] **F9-06** Agente público `POST /ia/consultas`: prompt del sistema, respuesta "no lo sé" sin contexto, solo datos públicos.
  - API pública acotada (1000 caracteres, 8 KiB HTTP, 5 solicitudes/minuto/IP), sesión UUID para agrupar consultas sin recuperar historial privado. Citas extractivas verificadas y SIN_CONTEXTO sin llamada al LLM cuando faltan fuentes. IA deshabilitada 422; proveedor/cuota 422/429. Prueba focalizada de respuesta con/sin contexto aprobada; build limpio.
- [x] **F9-07** Consulta de datos estructurados públicos (eventos en venta, precios, recintos).
  - Puerto acotado de lectura sin usuarios/compras/finanzas. Hasta cinco eventos EN_VENTA futuros con ventana de venta vigente y recinto/categoría activos; precios de entradas/localidades activas, sin prometer cupo. Fuente enlazada al detalle público de catálogo. Prueba focalizada de visibilidad aprobada y build limpio.
- [x] **F9-08** `logs_agente_ia`: consulta, respuesta, fuentes, tiempos, modelo, tokens, estado.
  - Decorador registra consultas procesadas, errores y cancelación (mejor esfuerzo de 3 segundos); fuentes camelCase, duración monotónica/fecha UTC. Sin identidad ni consulta a usuarios en este endpoint público. Credenciales aparentes y consultas rechazadas se omiten; respuestas solo se entregan tras guardar su log. Rechazos HTTP previos al caso de uso (validación/rate limit) quedan en logs HTTP sin cuerpo. Prueba focalizada de persistencia/citas e inmutabilidad aprobada.
- [x] **F9-09** Defensas: rate limit, longitud máxima, tratamiento de contenido no confiable, pruebas de prompt injection.
  - Rechazo previo a proveedores de consultas privadas/ajenas, inyecciones y credenciales aparentes. Documentos inseguros no se envían a embeddings; contexto y citas se filtran. Rate limit configurable 1–60/minuto/IP (default 5), concurrencia máxima 2 por proceso, límites HTTP/texto. Sin herramientas ni historial privado. Siete casos focalizados aprobados; defensa por capas, no garantía de clasificación perfecta. CORS admite PATCH para moderación desde frontend.
- [x] **F9-10** Documentos de negocio para ingestar (reglamento, reembolsos, FAQ, guía de carreras).
  - Cuatro documentos públicos en docs/rag y guía de carga explícita. Describen reglas implementadas y distinguen simulación de pagos; no inventan condiciones comerciales, horarios ni requisitos de carreras. Sin datos personales, credenciales o ingesta automática. Revisión documental contra reglas de negocio; no aplica nueva ejecución de pruebas.
- [x] **F9-11** Pruebas con LLM *fake* (deterministas) y una evaluación manual con preguntas de ejemplo.
  - Dos pruebas integrales API/BD con proveedores fake aprobadas; una suite completa final de 524 casos, 0 fallos/omitidos (2m46s), build sin warnings y formato limpio. Evaluación real acotada: dos embeddings (1536 dimensiones, coseno 0.8122) y una respuesta con cita literal válida (514 entrada/100 salida), sin reintentos. No se cambiaron credenciales del administrador local; requiere cambio inicial e ingesta explícita. Evidencia, revisión de criterios/AGENTS y límites en [VALIDACION_FASE_9.md](VALIDACION_FASE_9.md); guía vigente [IA_RAG.md](IA_RAG.md).

**Salida:** el agente responde con fuentes citadas, se niega a lo que no sabe y deja trazas.

Fase 9 cerrada localmente. A petición del usuario, se detiene implementación antes de reportes/Power BI; Fase 10 y D-09/Fase 11 siguen pendientes. El análisis de endpoints, vistas y flujos web solicitado se entrega en [ANALISIS_FRONTEND.md](ANALISIS_FRONTEND.md), con inventario de 80 operaciones reales en [ENDPOINTS.md](ENDPOINTS.md); no marca tareas de frontend como implementadas. CI remoto se verifica al publicar el PR.

### Extensión D-08 — localidades numeradas (antes de Fase 10)

Ampliación aprobada por el usuario el 29/10/2026; excepción al orden original y a la pausa posterior a F9 únicamente para EXT-NUM. Reportes/Power BI no se adelantan. Diseño vinculante y contratos: [LOCALIDADES_NUMERADAS.md](LOCALIDADES_NUMERADAS.md). Esquema versionado V12: 31 tablas; las ventas numeradas aún no están habilitadas. Rutas y migración nuevas se registran aquí antes de programar. Las casillas históricas de F1–F9 no se reabren.

- [x] **EXT-NUM-01** Registrar D-08, esquema, restricciones, contratos API, alcance y tareas en AGENTS, BASE_DE_DATOS y este plan; documentación sin cambio funcional.
- [x] **EXT-NUM-02** Nueva migración de cuatro tablas, permisos, índices/FKs/triggers de integridad; mapeos EF y dominio; compatibilidad con generales. Regenerar diccionario real y conteos tras migrar.
  - V12 validada con Flyway en PostgreSQL temporal: 31 tablas/235 columnas contrastadas con EF; 11 pruebas focalizadas de infraestructura y dos de selección Domain aprobadas. Publicación inmutable, cupo incoherente y reserva sin asignación rechazados. Sin aplicación a BD local ni endpoints nuevos todavía.
- [x] **EXT-NUM-03** Configuración BORRADOR, publicación inmutable, disponibilidad pública paginada, bloqueo/desbloqueo operativo; ownership, auditoría, Swagger y límites del contrato.
  - Seis rutas configuradas; prueba HTTP/BD de propiedad, permisos, publicación, paginación y bloqueo íntegro de mesa aprobada. Swagger esperado: 86 operaciones. Sin activar venta numerada hasta EXT-NUM-05.
- [x] **EXT-NUM-04** Compra mixta con asientoIds, reserva indivisible, cupos conciliados e idempotencia; conservar orden asiento/asistente; venta numerada deshabilitada hasta integrar Worker.
  - Prueba HTTP/PG: compra mixta con rollback íntegro ante agotado, posiciones duplicadas, selección obligatoria, dos solicitudes concurrentes por mesa (una 202/otra 409) e idempotencia con mismos IDs. Total D-15 verificado. No desplegar hasta EXT-NUM-05.
- [x] **EXT-NUM-05** Integrar emisión/QR/consulta, cancelación, expiración, fallo, recuperación y reembolso; confirmar asignaciones junto con ledger/boletos; habilitar solo con API/Worker compatibles.
  - API/BD y servicio del consumidor: QR y ubicación por persona; cancelación repetida, rechazo, carrera procesar/expirar/cancelar, emisión repetida, escaneo único y reembolso mixto VALIDO/USADO. Ledger conciliado. Pasarela fake sin red. Suite completa de cierre en ejecución; no hay venta numerada desplegada todavía.
- [x] **EXT-NUM-06** Integración frontend de selección de sillas/mesa completa, disponibilidad, conflictos 409 y ubicación por boleto; revisar trabajo local existente sin sobrescribirlo.
  - Excepción explícita del usuario: frontend exclusivamente local, nunca versionado. Selector Vue local, mesa completa, asientoIds, recarga ante 409 y ubicación por boleto; build local y dos pruebas UI aprobadas. Solo se versiona GUIA_NUMERACION.md; D-09/F11 siguen pendientes. Exclusión local en .git/info/exclude, sin incorporar archivos del frontend al PR.
- [x] **EXT-NUM-07** Cerrar pruebas integradas/concurrencia/idempotencia, documentación de demo y RAG, revisión de diff y PR; sin reportes/Power BI.
  - Cierre: 528 pruebas aprobadas (0 fallidas/omitidas), build sin warnings y formato limpio; caso numerado repetido tras ordenar bloqueos. V12/V13 aplicadas y validadas en PostgreSQL local (31 tablas), API Healthy y Swagger 86. Frontend solo local: build y dos pruebas UI aprobadas, excluido de Git. Evidencia, límites y revisión §§5/12 en [VALIDACION_NUMERACION.md](VALIDACION_NUMERACION.md). PR remoto/CI a cargo del usuario.

Plan inicial de PRs: A documentación (EXT-NUM-01); B esquema/configuración (02–03); C ciclo de compra completo (04–05); D frontend y cierre (06–07). Excepción posterior aprobada el 29/10/2026: preparar 02–07 en una rama local basada en A mientras su CI sigue pendiente, sin esperar merge; PR de backend unificado y commits por tarea. Frontend solo local excluido; en 06 se versiona únicamente su guía. Cada rama desde develop actualizado después del merge previo del usuario; no push/merge del agente. Una tarea/commit con Refs. Pruebas focalizadas por cambio y suite completa al cierre. Salida: no doble asignación, reservas múltiples indivisibles, liberación una vez por propietario, boletos históricos correctos y generales sin regresiones.

### Fase 10 — Reportería y BI
- [x] **F10-01** Infraestructura de reportes: contrato común (filtros, agrupación, totales, paginación) y `GET /reportes/{codigo}`.
  - Contrato `/api/v1/reportes/{codigo}` exclusivo de ADMINISTRADOR habilitado, también verificado en Application. Consultas parametrizadas de vistas `bi`, snapshot consistente, agrupaciones fijas, filtros incompatibles rechazados y totales por moneda independientes de la página. Los códigos se incorporan en F10-02–04. Prueba focalizada de validación aprobada.
- [x] **F10-02** Reportes R-01 … R-05.
  - Ventas brutas agrupadas y ocupación ponderada desde vistas bi; prueba API con venta 369.60 GTQ, ocupación 3/5, 401/403, filtros incompatibles y totales conservados en página vacía aprobada.
- [x] **F10-03** Reportes R-06 … R-10.
  - Asistencia, ledger neto por dueño, comisiones, clientes y compras no exitosas. Prueba API antes/después de reembolso aprobada: 3 boletos anulados, netos 300/30 → 0/0 y ventas brutas conservadas en 369.60 GTQ.
- [x] **F10-04** Reportes R-11 … R-15.
  - Calificaciones ponderadas, eventos, carreras, IA y auditoría/notificaciones. Prueba API focalizada aprobada, sin conversaciones expuestas. R-14 informa tiempo por grupo diario; total de tiempo null porque la vista no conserva el conteo de mediciones no nulas. R-15 separa dos secciones y etiqueta participaciones de usuarios (no únicos globales). Sin cambios de esquema.
- [x] **F10-05** Exportadores Excel (ClosedXML) y PDF (QuestPDF) detrás de `IReportExporter`; auditoría de `EXPORT`.
  - XLSX/PDF abiertos y verificados con ClosedXML/PdfPig desde API, total 369.60 y dos auditorías persistidas. Descarga completa filtrada (sin paginación), límites 10000/500 filas por sección, 422 sin parcial, archivos temporales eliminados al cerrar. Licencias y uso local documentados en [REPORTES.md](REPORTES.md).
- [ ] **F10-06** Power BI: cadena de conexión a `bi` con `bi_reader`, archivo `.pbix` de ejemplo y guía.
  - Implementación preparada: proyecto versionable bi/Promotora.pbip (16 vistas, Resumen + 15 páginas, medidas DAX), Desktop gratuito instalado, LOGIN local promotora_bi/bi_reader creado a solicitud del usuario y permisos/autenticación TCP verificados. Clave solo en .env.powerbi ignorado. Validación final de archivos Microsoft aprobada. El usuario eligió comprobar apertura/actualización y guardar el PBIX personalmente: pendiente esa comprobación y copia .pbix; no se marca completada. Ver [POWER_BI.md](POWER_BI.md).
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

- [x] **R-01** Ventas por evento
- [x] **R-02** Ventas por categoría
- [x] **R-03** Ventas por recinto
- [x] **R-04** Ventas por rango de fechas y método de pago
- [x] **R-05** Ocupación por localidad
- [x] **R-06** Boletos vendidos vs. usados (asistencia)
- [x] **R-07** Ingresos por dueño de recinto
- [x] **R-08** Comisiones de la plataforma
- [x] **R-09** Top clientes
- [x] **R-10** Compras fallidas, canceladas y expiradas
- [x] **R-11** Calificación promedio por presentador
- [x] **R-12** Eventos por estado
- [x] **R-13** Inscritos por distancia en carreras (5K/10K/21K/42K)
- [x] **R-14** Uso del agente de IA (consultas, tiempos, tokens)
- [x] **R-15** Auditoría de operaciones y notificaciones por estado

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
| Cuotas/latencia de Ollama Cloud y Gemini API | Proyectos gratuitos sin facturación, límites de contexto/salida, lotes y rate limit; HTTP/LLM fake en pruebas sin consumo real. Sin modelos locales, Batch API diferida, reintentos automáticos ni fallback pagado. El backend no puede comprobar el nivel de facturación de la clave; 429 controlado y cuota verificable en el panel del proveedor |
| Cambio de modelo de embeddings | La dimensión del vector queda fijada; reindexar si cambia |
| Alcance ampliado D-08 | EXT-NUM aprobado por etapas; no habilitar ventas numeradas hasta integrar API/Worker y probar el ciclo completo; reportes/Power BI pendientes |
| Pasarela real no disponible | Adaptador simulado detrás de puerto (D-10) |
| Licencias de paquetes | Revisar cada NuGet nuevo (ver `AGENTS.md` §2) |
| IVA y alcance fiscal | Tasa configurable en appsettings y cálculo en Domain; conciliación de los tres componentes del cobro. Facturación electrónica fuera de alcance |
| Reembolso parcial | Fuera del alcance actual; solo reembolso total que revierte movimientos registrados, sin recalcular tasas |
| Compras atascadas o carreras de estado | F6-11: timeout configurable, consulta de pago por public_id y recuperación idempotente; toda transición usa estado esperado y solo el ganador devuelve cupo |
