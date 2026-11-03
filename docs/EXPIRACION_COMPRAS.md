# Expiración de reservas — F5-08/F6-07

## Qué está implementado

Application/Ticketing contiene ExpirarComprasCommand/Handler y el puerto IExpiracionCompraRepository. Infrastructure/Ticketing consulta PostgreSQL y reutiliza la devolución atómica de F5-02. API/Worker registran la operación interna en DI, sin depender de un actor HTTP.

El handler no es un endpoint. F6-07 añade ExpiracionComprasService en Worker: ejecuta al arrancar y espera un intervalo después de cada lote, con un scope nuevo por ciclo y sin ejecuciones superpuestas dentro del mismo host. Por defecto está deshabilitado; activarlo requiere conexión PostgreSQL externa. No requiere RabbitMQ ni SMTP para este job.

Cada ejecución captura una fecha UTC con IClock y procesa un lote de 100 por defecto, entre 1 y 1000. Selecciona solo PENDIENTE con expira_en <= ahora, ordenadas por vencimiento e ID. PROCESANDO, COMPLETADA y reservas aún vigentes quedan fuera. El lote acota trabajo interno, sin añadir una regla comercial ni una columna.

La selección es una lista de candidatos: por cada compra el UPDATE revalida estado PENDIENTE y vencimiento. Solo quien afecta una fila la cambia a EXPIRADA y devuelve sus cantidades por tipo de entrada. La auditoría UPDATE, con usuario null del proceso interno, comparte transacción con estado/cupo. Un error revierte el lote; el adaptador existente reintenta conflictos transaccionales. Repetir la ejecución no vuelve a devolver cupo ni duplica auditorías. No se generan pagos, reembolsos, boletos ni mensajes.

Si cancelar o tomar para PROCESANDO gana después de la consulta, la expiración afecta cero filas y omite esa compra. Su respuesta interna ExpiracionLoteDto informa Revisadas, Expiradas y OmitidoPorBloqueo, sin datos personales.

Infrastructure ejecuta pg_try_advisory_xact_lock(namespace PROM=0x50524F4D, tarea=607) después de iniciar la transacción y antes de consultar candidatos. Si otra instancia tiene ese bloqueo, devuelve un lote omitido (0/0) inmediatamente, sin tocar compras ni cupo. Todos los hosts que apuntan a la misma BD comparten la clave fija; no es configurable por instancia. El bloqueo dura hasta commit, rollback o disposición de la transacción, incluyendo la auditoría. No utiliza tablas nuevas ni un lock de sesión que pueda quedar retenido en el pool. [Semántica oficial de PostgreSQL](https://www.postgresql.org/docs/current/functions-admin.html#FUNCTIONS-ADVISORY-LOCKS).

La coordinación no sustituye el UPDATE condicional: cancelación y procesamiento no toman este advisory lock y pueden ganar una transición. El vencimiento por sí solo no cambia una PROCESANDO; su recuperación pertenece a F6-11. Un fallo del lote revierte todo y el host reintenta en el siguiente ciclo; shutdown cancela consulta/handler/espera y dispone el scope. Logs solo con conteos o aviso genérico, sin compras, SQL ni credenciales.

## Activar Worker en tu entorno

Configurar Compose y ConnectionStrings:Promotora del Worker con el login restringido según [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). Si quieres ejecutar únicamente expiración, puedes dejar Outbox:Enabled y Notificaciones:Enabled en false; Compras:ConsumerEnabled sigue false hasta F7. En la terminal del Worker, comandos separados:

```powershell
$env:Expiracion__Enabled = 'true'
```

```powershell
$env:Expiracion__BatchSize = '100'
```

```powershell
$env:Expiracion__PollIntervalSeconds = '30'
```

```powershell
dotnet run --project src/Promotora.Worker
```

BatchSize admite 1–1000; PollIntervalSeconds 1–300. Un lote por ciclo: el retraso efectivo puede superar el intervalo si hay acumulación de pendientes, trabajo prolongado o BD no disponible. No cambiar expiraEn ni estados mediante SQL para acelerar la prueba. No se crean nuevos usuarios/contraseñas ni secretos para este job.

## Verificación local

Con Docker funcionando, desde la raíz; las pruebas usan PostgreSQL temporal y no modifican Compose:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*ExpiracionComprasTests'
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*ExpiracionWorkerTests'
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Las pruebas verifican vencimiento exacto, lote, omisión de PROCESANDO/COMPLETADA/vigentes, diez handlers simultáneos, carreras forzadas entre consulta y UPDATE, rollback ante fallo de auditoría y resolución en Worker sin usuario HTTP. F6-07 añade dos hosts reales sobre PostgreSQL, omisión con lock ocupado, liberación tras commit/rollback/dispose, procesamiento por lotes, reintento con scope nuevo, shutdown durante auditoría y validación de opciones/cancelación. Usan reloj fijo y esperas async acotadas; no usan Sleep ni requieren modificar compras locales.

## Swagger y frontend

Este bloque no añade endpoints ni cambia contratos de Swagger. En otra terminal arranca la API con su configuración habitual; autenticación en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md):

```powershell
dotnet run --project src/Promotora.WebApi
```

Como CLIENTE, crea una compra de prueba con cupo disponible y una Idempotency-Key nueva; conserva su publicId y expiraEn. No la canceles. Consulta GET /api/v1/compras/{publicId}: al vencer y procesarse un ciclo del Worker, mostrará EXPIRADA y el cupo volverá al catálogo. Reserva predeterminada: 15 minutos; considerar el intervalo y la carga del job. Dos Workers con la misma conexión no duplicarán devolución ni auditoría. Si el job está deshabilitado o detenido, el tiempo por sí solo no cambia el estado; al reiniciarlo revisa vencidas. Puede cancelar una PENDIENTE según [COMPRAS_API.md](COMPRAS_API.md).

No cambia configuración de usuario/contraseña ni añade secretos. Cobro/emisión HTTP y criterio de salida completo de Fase 5 siguen pendientes de la integración financiera F7-02/F7-03.
