# Carga de compras asíncronas — F6-09

## Alcance

`CompraCargaApiTests` ejecuta dos escenarios, 500 y 1.000 solicitudes HTTP concurrentes, contra un evento con 100 entradas de Q100. Ocho clientes autenticados comparten el cupo; todas las solicitudes tienen una clave de idempotencia distinta y esperan la misma barrera de salida. Se usa WebApplicationFactory/TestServer con los controllers y handlers reales, PostgreSQL/pgvector migrado con Flyway y RabbitMQ de Testcontainers.

Un relay publica outbox mientras el consumidor real completa las compras usando la pasarela simulada, emisión QR, ledger y auditoría. No se reemplazan los efectos financieros con dobles. El pool de conexiones de esta prueba tiene máximo 32 conexiones, espera de conexión/comando de 120 segundos y límite global de cuatro minutos. Las 500/1.000 solicitudes HTTP están en vuelo simultáneamente; el pool controla las conexiones físicas, no reduce el número de solicitudes. Estos ajustes viven solo en memoria en la prueba. La API de prueba admite hasta 1.000 solicitudes de compra por ventana; no se cambian los límites de producción.

## Criterios de aprobación

- Exactamente 100 respuestas 202 Accepted y 400/900 respuestas 409 Conflict; ningún 500, 429 ni compra parcial.
- Cien compras distintas COMPLETADA, cupo disponible cero, cien pagos APROBADO y cien boletos.
- Tres movimientos por compra, suma igual al total congelado; cada venta suma Q123.20 (100 + 10 + 13.20). Las cien ventas suman Q12,320.00.
- Todos los saldos igualan SUM(ledger), incluso después de ejecutar ambos escenarios en la misma base temporal.
- Cien registros de deduplicación correspondientes a los outboxes de compra y cien outboxes de confirmación; retry/DLQ vacías.

No se ejecuta el envío de correo en esta prueba. Las colas de notificaciones pueden conservar mensajes; las pruebas de correo están separadas. F6-08 verifica reentrega, rollback y deduplicación frente a fallos.

## Ejecutar localmente

Desde la raíz del repositorio, con Docker Desktop operativo. No necesitas usuarios, contraseñas ni user-secrets: los actores, claves y contenedores son temporales y se generan en memoria.

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter FullyQualifiedName~CompraCargaApiTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

La salida de cada escenario queda en el TRX de `TestResults` (no versionado), con la línea `CARGA`: solicitudes, aceptadas, conflictos, duración hasta terminar las reservas HTTP, duración hasta verificar el flujo completo y percentiles p50/p95/p99 de las respuestas HTTP. El tiempo completo incluye consultas de verificación; no mide solo el consumidor.

## Interpretación

Ejecución aislada del 11 de octubre de 2026, Windows, .NET 10, configuración Release, Docker Desktop con PostgreSQL/pgvector 16 y RabbitMQ de las fixtures. Resultado: 2 pruebas aprobadas, 0 fallidas y 0 omitidas; duración total 55.8 segundos incluyendo contenedores y preparación.

| Solicitudes | 202 | 409 | Reservas HTTP (ms) | Flujo + verificación (ms) | p50 (ms) | p95 (ms) | p99 (ms) |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 500 | 100 | 400 | 5692 | 15838 | 4413 | 5296 | 5381 |
| 1000 | 100 | 900 | 5031 | 10462 | 3276 | 4784 | 4910 |

Cada fila confirmó 100 pagos, 100 boletos, 300 movimientos y suma Q12,320.00. La segunda ejecución aprovecha el entorno ya iniciado; que resulte más rápida no demuestra que más carga mejore el rendimiento.

Estos resultados prueban integridad y carga básica reproducible en el entorno local. TestServer opera en memoria; no mide red externa, TLS, balanceador, una pasarela externa ni entrega SMTP. Los tiempos dependen de CPU, Docker, arranque y otras pruebas concurrentes. No constituyen un SLA ni una estimación de capacidad en producción. Para observar la compra en Swagger y el Worker local, consulta [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md) y [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md).

Desde F6-11, estos escenarios también usan el proveedor durable de V11. Las mediciones de la tabla corresponden al bloque original F6-09 con el simulador en memoria; no se presentan como tiempos del adaptador nuevo. Recuperación y consulta durable tienen sus pruebas propias en [RECUPERACION_COMPRAS.md](RECUPERACION_COMPRAS.md).
