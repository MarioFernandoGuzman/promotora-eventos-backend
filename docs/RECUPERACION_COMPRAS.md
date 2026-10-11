# Recuperación de compras y pago durable — F6-11

## Módulos y comportamiento

Finance incorpora `PasarelaSimuladaDurable`: API y Worker usan `pagos_simulados` de V11 como diario del proveedor. El resultado se confirma por autocommit en una conexión distinta antes de devolverse. Referencia única = compras.public_id; el primer resultado conserva monto, moneda, estado y fecha incluso después de reiniciar o cambiar la tasa simulada. Una referencia con otro monto devuelve conflicto. No hay tarjeta ni cobro externo; `FakePaymentGateway` queda para cálculo y pruebas puras.

Ticketing incorpora `RecuperarComprasCommand/Handler` y el puerto `IRecuperacionCompraRepository`. Worker añade `RecuperacionComprasService`, opt-in, un scope por ciclo y esperas cancelables. No hay endpoints nuevos ni cambios del contrato Swagger; los clientes continúan consultando Location o GET /api/v1/compras/{publicId}.

El job busca PROCESANDO con procesando_desde <= ahora UTC menos el timeout. Consulta candidatas relacionadas con el outbox original para recuperar asistentes y su identidad. Dentro de una transacción registra deduplicación provisional y revalida estado/timeout con FOR UPDATE SKIP LOCKED. Otro recuperador se serializa por el mismo mensaje; un lock ocupado, estado final o error esperado revierte el marcador provisional.

| Resultado de la consulta | Acción |
|---|---|
| APROBADO consistente | Retoma/inicia el intento y usa el finalizador existente: pago, VENTA/COMISION/IMPUESTO, saldos, COMPLETADA, boletos, auditoría y outbox juntos |
| RECHAZADO consistente | Registra rechazo, UPDATE condicional PROCESANDO → FALLIDA y devuelve cupo una vez, con auditoría y deduplicación |
| Ausencia confirmada en el diario durable, sin efectos financieros/boletos existentes | FALLIDA y devolución única; no se inventa un pago rechazado |
| Error de conexión, cancelación o fallo inesperado | Rollback; conserva PROCESANDO y cupo; el host reintenta con un scope nuevo |
| Montos contradictorios, outbox inválido o efectos históricos inconsistentes | Omite sin devolver cupo; requiere conciliación |

La recuperación **nunca llama CobrarAsync**. Null solo significa ausencia si la consulta al diario durable terminó correctamente; la memoria vacía del simulador puro no autoriza devolución. Un pago histórico APROBADO/REEMBOLSADO, ledger o boletos sin respuesta aprobada consistente conservan PROCESANDO. No existe un endpoint administrativo de conciliación en este alcance.

El consumidor normal sigue tomando únicamente PENDIENTE. Si la toma afecta cero filas, omite sin guardar una deduplicación nueva: una entrega de PROCESANDO no debe bloquear al recuperador con un marcador sin efectos. Si una compra PENDIENTE ya tiene ese marcador confirmado, se revierte y requiere conciliación.

El flujo normal simulado conserva toma y finalización en una única transacción: PROCESANDO normalmente no es visible antes del resultado final. Un fallo revierte a PENDIENTE y usa retry/expiración existentes; el diario simulado permanece y permite consultar el mismo resultado tras reinicio. El job atiende PROCESANDO **persistidas**, preparadas explícitamente en pruebas. Un registro del diario sin efectos de compra no es por sí solo una venta ni un movimiento de dinero. No sustituir el simulador por una pasarela externa con este protocolo: requiere diseñar toma durable, cobro fuera de la transacción y conciliación de todos los estados.

## Preparar y activar localmente

Desde la raíz, con conexiones y secretos actuales de [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md) y [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md). No cambia el usuario ni la contraseña de API/BD/RabbitMQ. No se escribe ninguna credencial en la migración.

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
docker compose run --rm flyway info
```

Debe aparecer V11 aplicada. En la terminal del Worker conserva ConnectionStrings__Promotora, Boletos__SigningKey y Boletos__CategoriaCarreraId ya configuradas. La firma QR debe ser la misma de los boletos vigentes. El job de recuperación no requiere RabbitMQ para consultar/finalizar: escribe confirmación en outbox y el relay la publica después.

```powershell
$env:Recuperacion__Enabled = 'true'
```

```powershell
$env:Recuperacion__TimeoutMinutos = '5'
```

```powershell
$env:Recuperacion__BatchSize = '100'
```

```powershell
$env:Recuperacion__PollIntervalSeconds = '30'
```

```powershell
dotnet run --project src/Promotora.Worker
```

Timeout permitido: 1–1440 minutos; lote: 1–1000; intervalo: 1–300 segundos. Por defecto el job está deshabilitado. Para el flujo completo, activa también outbox, consumidor de compras y notificaciones con las guías existentes. Los logs muestran conteos de completadas/fallidas/omitidas; nunca cuerpos, QR ni secretos. Un ciclo sin candidatas no genera ventas ni correos.

## Pruebas y resultados esperados

Con Docker Desktop operativo, sin iniciar API/Worker ni alterar tu base local:

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter "FullyQualifiedName~RecuperacionComprasTests|FullyQualifiedName~PasarelaDurableTests" --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Testcontainers migra una base PostgreSQL/pgvector aislada. Las pruebas cubren veinte instancias del proveedor, consulta desde una instancia nueva con tasa cambiada, conflicto sin sobrescritura, rollback de negocio con diario persistente y permisos INSERT-only. Recuperación prueba dos instancias, aprobación/rechazo/ausencia sin nuevo cobro, rollback después de ledger, consulta indisponible, timeout/lock, pago histórico sin diario y host periódico con shutdown.

Seis carreras lanzan simultáneamente procesamiento real, expiración, transición condicional de cancelación y recuperación, desde PENDIENTE o PROCESANDO persistida. Comprueban un estado final y cupo tres si se vendieron dos de cinco entradas, o cinco si se devolvió la reserva; nunca siete. La cancelación concurrente ejercita el puerto de cupo, no HTTP; autorización y 409 de cancelación tienen pruebas API separadas. Se verifican pagos, ledger, firmas QR, outbox, deduplicación y conciliación. Las pruebas F6-08/F6-09 también usan ahora el proveedor durable.

No preparar compras atascadas con SQL manual en tu base local: los escenarios aislados permiten repetir fallos y carreras. En Swagger usa tus cuentas actuales y el flujo normal de [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md).

Verificación del 11 de octubre de 2026: 360 pruebas Release aprobadas, ninguna fallida u omitida; build sin warnings y formato limpio. Dieciocho casos nuevos (cuatro de proveedor durable y catorce de recuperación). La suite aplica V11 con Flyway, consulta las 27 tablas/203 columnas con el rol restringido y comprueba readiness, arquitectura, RabbitMQ, correos y carga con el adaptador durable.
