# Expiración de reservas — F5-08

## Qué está implementado

Application/Ticketing contiene ExpirarComprasCommand/Handler y el puerto IExpiracionCompraRepository. Infrastructure/Ticketing consulta PostgreSQL y reutiliza la devolución atómica de F5-02. API/Worker registran la operación interna en DI, sin depender de un actor HTTP.

El handler no es un endpoint. El plan separa su implementación (F5-08) de la ejecución periódica y coordinación de instancias en Worker (F6-07). Arrancar Worker hoy no ejecuta automáticamente la expiración; no se añadió un temporizador ni un comando de consola fuera del plan.

Cada ejecución captura una fecha UTC con IClock y procesa un lote de 100 por defecto, entre 1 y 1000. Selecciona solo PENDIENTE con expira_en <= ahora, ordenadas por vencimiento e ID. PROCESANDO, COMPLETADA y reservas aún vigentes quedan fuera. El lote acota trabajo interno, sin añadir una regla comercial ni una columna.

La selección es una lista de candidatos: por cada compra el UPDATE revalida estado PENDIENTE y vencimiento. Solo quien afecta una fila la cambia a EXPIRADA y devuelve sus cantidades por tipo de entrada. La auditoría UPDATE, con usuario null del proceso interno, comparte transacción con estado/cupo. Un error revierte el lote; el adaptador existente reintenta conflictos transaccionales. Repetir la ejecución no vuelve a devolver cupo ni duplica auditorías. No se generan pagos, reembolsos, boletos ni mensajes.

Si cancelar o tomar para PROCESANDO gana después de la consulta, la expiración afecta cero filas y omite esa compra. El scheduler de F6-07 deberá añadir la coordinación entre instancias; aun sin esa coordinación, el handler mantiene la devolución única. Su respuesta ExpiracionLoteDto informa Revisadas y Expiradas, sin datos personales.

## Verificación local

Con Docker funcionando, desde la raíz; las pruebas usan PostgreSQL temporal y no modifican Compose:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*ExpiracionComprasTests'
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Las pruebas verifican vencimiento exacto, lote, omisión de PROCESANDO/COMPLETADA/vigentes, diez handlers simultáneos, carreras forzadas entre consulta y UPDATE, rollback ante fallo de auditoría, cancelación del token y resolución en Worker sin usuario HTTP. Usan reloj fijo; no esperan con Sleep ni requieren modificar compras locales.

## Swagger y frontend

Swagger conserva 59 operaciones. El cliente sigue consultando GET /api/v1/compras/{publicId}; cuando el proceso de fondo cambie la compra, la respuesta mostrará EXPIRADA. Hasta activar F6-07, expiraEn se guarda pero el simple transcurso del tiempo no cambia el estado. Puede cancelar una PENDIENTE según [COMPRAS_API.md](COMPRAS_API.md). No forzar estados ni cupo mediante SQL para probar.

No cambia configuración de usuario/contraseña ni añade secretos. Cobro/emisión HTTP y criterio de salida completo de Fase 5 siguen pendientes de la integración financiera F7-02/F7-03.
