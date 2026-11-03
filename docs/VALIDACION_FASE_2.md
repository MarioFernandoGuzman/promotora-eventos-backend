# Validación de Fase 2

Rama: `feature/f2-esqueleto-solucion`, creada desde `develop` tras el merge del PR #9. Alcance aprobado: F2-01 a F2-07, en orden. Commits locales por tarea y una corrección de persistencia identificada durante la revisión. Sin push ni merge del agente.

## Resultado local

- .NET SDK 10.0.302; Docker Desktop con contenedores Linux; Compose 5.5.1.
- Restore y build Release con `-warnaserror`: sin advertencias ni errores.
- `dotnet format --verify-no-changes --no-restore`: limpio, con LF consistente entre sistemas.
- Suite completa: 41 pruebas, cero fallos y cero omitidas. TRX por cada proyecto en `TestResults/`.
- Testcontainers aplica las migraciones SQL reales mediante Flyway sobre `pgvector/pgvector:pg16`; la aplicación consulta con un login miembro de `promotora_app`, sin privilegios de superusuario.
- `scripts/Test-Compose.ps1 -WithApi`: PostgreSQL/RabbitMQ saludables, MailHog y RabbitMQ management responden, Flyway migrate/validate/info con 11 migraciones Success (V1–V10 y R__bi_vistas), Kestrel con `live`/`ready` 200, Swagger UI 200 y documento v1 con `paths: {}`. Recursos temporales retirados sin borrar volúmenes locales.

## Cobertura por proyecto

| Proyecto | Pruebas | Evidencia |
|---|---:|---|
| Domain.Tests | 3 | Consistencia de Result, errores, copia inmutable y acceso al valor |
| Application.Tests | 4 | Decoradores de commands/queries, validación antes del handler y cancelación |
| Infrastructure.Tests | 11 | 26 tablas/198 columnas/16 vistas consultables, defaults/identity, perfil diferido, permisos, JSON, clave compuesta, vector/coseno, UTC, commit/rollback y readiness |
| Api.Tests | 12 | Swagger vacío en Development, oculto en Production; health sin conexión; correlación; ProblemDetails 400/404/500 y mapa 400/401/403/404/409/422/429; camelCase |
| Architecture.Tests | 11 | Grafo de proyectos, dependencias por capa, Domain sin paquetes, controllers aislados y base `/api/v1` |

Se detectó y corrigió que EF omitía valores CLR `false`/`0` al insertar columnas con defaults SQL `true`/`1`/`10`. Los modelos inicializan esos defaults y usan sentinels explícitos; las regresiones comprueban que `activo=false` y `version=0` se conservan. No se añadió un CHECK de versión: ese campo sigue exactamente el esquema existente.

## Revisión contra plan y AGENTS.md §§5 y 12

- Diez proyectos, capas separadas; puertos en Application y adaptadores/EF en Infrastructure. Sin paquetes en Domain ni referencias invertidas.
- No se modifican migraciones, tablas, columnas ni vistas BI. Solo se agrega el aprovisionamiento del login local fuera de Flyway; roles de permisos existentes conservados.
- Modelos `*Row` son persistencia por módulo; no se exponen como DTO ni sustituyen futuras entidades del dominio. Los handlers de negocio, ownership, auditoría de negocio, pagos, cupos, QR y mensajería se implementarán en las fases previstas.
- No hay DELETE productivo sobre usuarios/transacciones/ledger/logs ni publicación dentro de transacciones. Los permisos de usuarios/ledger se probaron con login restringido.
- Fechas TIMESTAMPTZ en UTC; reloj inyectable; contratos async con CancellationToken. Firmas impuestas por middleware/xUnit/hosts utilizan los tokens y ciclos de vida del framework.
- Sin APIs de creación/migración EF, `.Result`, `.Wait()`, `async void`, `DateTime.Now` ni Thread.Sleep. Sin Node/Python, MediatR, AutoMapper, MassTransit ni FluentAssertions.
- Contraseñas, conexiones y API keys no se guardan en el repositorio. Credenciales de pruebas aleatorias solo en memoria; la API no expone mensajes internos en 500 ni registra payloads.
- Swagger está limitado a Development; API base y health checks documentados. El Worker es un host preparado: consumidores/relay/jobs corresponden a F6.
- Dependencias y licencias justificadas en `DEPENDENCIAS.md`; tareas marcadas en el plan; guía de pruebas y arranque en `DESARROLLO_LOCAL.md`.

Incumplimientos funcionales detectados en la revisión y pendientes: ninguno dentro del alcance F2. Limitación de entrega: GitHub Actions aún debe ejecutar los jobs `build-test` y `migraciones` en el PR publicado por el usuario; la verificación local no sustituye ese estado remoto. No iniciar Fase 3 hasta revisar y mergear este PR en develop.
