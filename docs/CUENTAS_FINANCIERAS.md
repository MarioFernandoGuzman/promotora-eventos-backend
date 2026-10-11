# Cuentas financieras — F7-01

## Componentes preparados

Domain/Finance contiene CuentaFinanciera y la regla de cierre: solo DUENO, saldo exactamente cero y fecha UTC. Repetir un cierre conserva fecha y no genera efectos adicionales. PLATAFORMA/IMPUESTOS y saldos positivos o negativos impiden cierre.

Application define IAltaCuentaDuenoPort, ICuentaRepository y CerrarCuentaCommand/Handler. El alta de identidad usa el puerto de Finance; no invoca un handler de otro módulo. El cierre exige actor activo, contraseña cambiada y rol administrador/dueño; valida propiedad en el handler. Cuenta ajena para un dueño devuelve NotFound.

Infrastructure implementa los puertos con PostgreSQL. Alta DUENO en GTQ mediante INSERT ON CONFLICT DO NOTHING dentro de la transacción de usuario/perfil; audita CREATE solamente al insertar. Un fallo de cuenta/auditoría revierte todo el alta. No crea una cuenta para clientes/administradores, ni reabre una cuenta CERRADA.

El cierre bloquea usuario/cuenta, aplica la regla de Domain y usa UPDATE condicional WHERE tipo=DUENO AND estado=ACTIVA AND saldo=0; guarda fecha_cierre y auditoría UPDATE en esa misma transacción. El trigger existente de V9 rechaza movimientos en CERRADA y coordina mediante lock de fila. Las cuentas PLATAFORMA/IMPUESTOS GTQ existentes de V10 conservan sus índices únicos por moneda; no se modifica ni reejecuta una migración histórica para implementar esta tarea.

La moneda de alta es GTQ, coherente con precios/catálogo actual. Los dueños previos que aún no tengan cuenta no se modifican al arrancar la API; el puerto interno de alta idempotente queda disponible para asegurar sus cuentas al integrar F7-03. No se añade un job de backfill, nueva tabla, columna, endpoint de creación manual ni configuración de credenciales.

## Contrato HTTP aprobado

POST /api/v1/cuentas/{id}/cerrar no recibe body y requiere Bearer de un dueño de la cuenta o administrador habilitado, con contraseña inicial cambiada. Devuelve 200 con CuentaDto al cerrar o repetir (misma fechaCierre); 400 para ID no positivo; 401 sin sesión válida (incluido JWT de usuario dado de baja); 403 por rol o contraseña inicial pendiente; 404 para cuenta ajena/inexistente; 409 para saldo distinto de cero o cuenta PLATAFORMA/IMPUESTOS. Los errores usan ProblemDetails. El contrato fue aprobado por el usuario antes de implementar el controller. Las consultas de estado de cuenta/extracto corresponden a F7-06 y el cobro/ledger a F7-02/F7-03.

Respuesta de ejemplo (los IDs y fecha dependen de la cuenta local):

```json
{"id":3,"tipo":"DUENO","duenoId":1,"moneda":"GTQ","saldo":0,"estado":"CERRADA","fechaCierre":"2030-01-01T00:00:00Z"}
```

## Probar con Swagger UI

1. Prepara tu infraestructura y user-secrets con [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md) y la guía de [Swagger autenticado](FRONTEND_SWAGGER_AUTENTICACION.md). Conserva las variables de entorno requeridas por Compose en esa terminal.
2. Ejecuta la API con el comando separado siguiente y abre http://localhost:5080/swagger:

```powershell
dotnet run --project src/Promotora.WebApi
```

3. Haz login como administrador, cambia la contraseña inicial si corresponde y vuelve a hacer login. En Authorize pega solo accessToken. Crea un dueño mediante POST /api/v1/usuarios/duenos-recinto con los campos mostrados en Swagger y una contraseña elegida localmente. Su cuenta GTQ se crea en esa misma operación. Para los dueños anteriores sin cuenta, el alta automática no es retroactiva.
4. Hasta F7-06, el ID de cuenta se puede consultar en tu PostgreSQL de desarrollo con esta lectura (no cambia datos):

```powershell
docker compose exec postgres psql -U postgres -d promotora -c "SELECT c.id AS cuenta_id, d.usuario_id, c.tipo, c.moneda, c.saldo, c.estado FROM cuentas_financieras c JOIN duenos_recinto d ON d.id = c.dueno_id ORDER BY c.id;"
```

5. En Cuentas → POST /api/v1/cuentas/{id}/cerrar, pulsa Try it out, introduce el cuenta_id de la cuenta de prueba con saldo cero y ejecuta. Debe responder 200 y CERRADA; al repetir, fechaCierre debe ser igual. El cierre persiste: usa una cuenta creada para esta prueba y no la destinada a ventas posteriores.
6. Para comprobar propiedad, inicia sesión como otro dueño, reemplaza el Bearer y prueba ese mismo ID: 404. Un CLIENTE recibe 403. Al retirar autorización se recibe 401. Un administrador recibe 409 si intenta cerrar una cuenta de sistema. Los escenarios de saldo distinto de cero y movimiento posterior al cierre se verifican en las pruebas automáticas, sin modificar manualmente saldos de la base local.

El ajuste de orden fue aprobado y registrado antes del código: F7-01 → F7-02 → F7-03, después retomar los pendientes de F6. No se habilita el consumidor financiero ni COMPLETADA con estos componentes de cuentas.

## Pruebas locales

Con Docker operativo, desde la raíz del repo, comandos separados:

```powershell
dotnet build --no-restore -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*CuentasFinancierasTests' --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Pruebas sobre PostgreSQL+pgvector temporal migrado por Flyway: alta transaccional, rollback, veinte altas/reintentos sin duplicar/reabrir, veinte cierres con una auditoría/fecha, saldo no cero, propietario ajeno/cliente/admin, cuentas de sistema protegidas y trigger de movimiento en cuenta cerrada. No modifican Compose ni requieren tus contraseñas reales. No forzar balances ni estados mediante SQL para verificar en tu base local.

La prueba de concurrencia comprueba también que todos los reintentos devuelven la misma fecha leída de PostgreSQL, cuya precisión temporal difiere de la de DateTime. Las pruebas API comprueban permisos y propiedad reales, fecha idempotente, saldo positivo/negativo, protección de cuentas del sistema y el contrato Swagger sin requestBody.

Verificación final de F7-01 (2026-10-10): build Release con cero warnings, formato limpio y suite completa de 287 pruebas aprobadas, cero fallos y cero omitidas. Sin paquetes nuevos ni modificaciones de migraciones.
