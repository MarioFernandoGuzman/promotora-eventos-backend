# Liquidaciones a dueños — F7-05

## Contrato y reglas

`POST /api/v1/cuentas/{id}/liquidar`, solo ADMINISTRADOR activo y con contraseña inicial cambiada. CLIENTE y DUENO_RECINTO reciben 403. El handler vuelve a comprobar al administrador en PostgreSQL bajo bloqueo; no confía únicamente en el JWT.

Header obligatorio `Idempotency-Key`: entre 1 y 128 caracteres ASCII alfanuméricos o `.`, `_`, `:`, `-`. Es sensible a mayúsculas y su ámbito es la cuenta. Body:

```json
{"monto":100.00}
```

El monto está expresado en la moneda de la cuenta, es positivo, admite como máximo dos decimales y no supera 9999999999.99 (NUMERIC(12,2)). No se redondea una solicitud inválida ni se recalculan impuestos/comisión: se retira saldo ya registrado.

Respuesta 200 tanto al crear como al repetir la misma clave/monto:

```json
{"movimientoId":42,"cuentaId":7,"monto":100.00,"moneda":"GTQ","fecha":"2026-10-11T18:00:00Z"}
```

`monto` es el importe positivo solicitado; el movimiento persistido es LIQUIDACION negativo, con compra_id NULL. La respuesta mantiene ID y fecha originales incluso si luego cambia el saldo o se cierra la cuenta. La misma clave con otro monto en esa cuenta da 409. La misma clave en otra cuenta identifica una operación independiente. Reutilizar una clave conserva su significado para siempre: generar una nueva para cada retiro nuevo y conservarla cuando se reintenta por timeout.

| HTTP | Caso |
|---|---|
| 200 | Liquidación registrada o repetición del mismo monto |
| 400 | ID/monto inválido, clave ausente/inválida o body inválido |
| 401/403 | Sin sesión, rol incorrecto, administrador inhabilitado o cambio de contraseña pendiente |
| 404 | Cuenta inexistente |
| 409 | Cuenta PLATAFORMA/IMPUESTOS o cerrada, saldo insuficiente/desconciliado, misma clave con otro monto |

Una operación nueva solo admite DUENO ACTIVA con `saldo >= monto` y `saldo == SUM(movimientos)`. Puede retirar todo el saldo; el cierre sigue siendo una operación separada. Un reintento confirmado no vuelve a retirar ni exige que la cuenta siga activa.

## Implementación y límites

Domain/Finance (`CuentaFinanciera.Liquidar`) valida cuenta/monto/saldo y devuelve el débito. Application/Finance incorpora command, validator, handler, DTO y `ILiquidacionRepository`. Infrastructure implementa el puerto con EF/PostgreSQL. `CuentasController` llama al handler; Swagger declara header obligatorio, ejemplo de body y códigos de error. No se añaden paquetes ni migraciones.

Orden transaccional: bloquear usuario, revalidar permisos, bloquear cuenta con FOR UPDATE, buscar la referencia, comprobar reglas/conciliación, descontar con UPDATE condicional (DUENO/ACTIVA/saldo suficiente), INSERT del movimiento y auditorías CREATE de movimiento / UPDATE de cuenta. Un fallo revierte todos los efectos, incluida la referencia de idempotencia.

Se reserva la descripción `liquidacion:v1:<SHA-256 hexadecimal de la clave UTF-8>` para estos movimientos. No se almacena la clave original. La búsqueda/inserción se serializa mediante el bloqueo de la cuenta; **no existe una restricción UNIQUE nueva**. Cualquier otro escritor de liquidaciones debe respetar este protocolo y evitar esa descripción reservada. No modificar manualmente movimientos o saldos para resolver un conflicto.

El ledger conserva solo INSERT. Liquidaciones y reembolsos compiten por el saldo bajo los mismos bloqueos de cuentas: si el retiro deja saldo insuficiente para un reembolso total, este devuelve 409 sin efectos. No se libera cupo, cambia el pago ni se envían mensajes al liquidar.

**Alcance:** registro contable simulado, sin transferencia bancaria, datos bancarios ni integración externa. Las consultas HTTP de cuentas/extractos de F7-06 se describen en [CONSULTAS_FINANCIERAS.md](CONSULTAS_FINANCIERAS.md).

## Probar localmente con Swagger

Conservar la configuración externa y las contraseñas actuales. Preparación desde cero: [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md). Autenticación: [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). Para generar ingresos reales del flujo simulado: [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).

Desde la raíz del repositorio, con las variables de Compose ya configuradas, ejecutar cada comando por separado:

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
dotnet run --project src/Promotora.WebApi
```

En otra terminal con los secretos del Worker preparados:

```powershell
dotnet run --project src/Promotora.Worker
```

1. Completar una compra pagada siguiendo la guía asíncrona y confirmar COMPLETADA. El dueño del recinto recibe el subtotal; comisión/IVA permanecen en las cuentas del sistema. Crear una venta de prueba específica para este escenario.
2. Obtener el ID con GET /api/v1/cuentas en Swagger (como administrador, filtrar tipo=DUENO). También se puede usar esta consulta de diagnóstico **solo lectura**:

```powershell
docker compose exec -T postgres psql -U postgres -d promotora -c "SELECT id, dueno_id, moneda, saldo, estado FROM cuentas_financieras WHERE tipo='DUENO' ORDER BY id;"
```

3. Abrir `http://localhost:5080/swagger`, login como ADMINISTRADOR y Authorize con el JWT. Si debeCambiarPassword es true, realizar antes el cambio indicado en la guía.
4. En Cuentas → POST liquidar, introducir ID, una clave nueva (por ejemplo `retiro-local-001`) y un monto menor o igual al saldo. Esperar 200 y guardar movimientoId/fecha.
5. Repetir con la misma clave y monto: debe devolver exactamente el mismo resultado. Cambiar solo monto: 409. Usar clave nueva y monto mayor al saldo restante: 409.
6. Comprobar saldo y movimientos sin editar datos:

```powershell
docker compose exec -T postgres psql -U postgres -d promotora -c "SELECT c.id, c.saldo, COALESCE(SUM(m.monto),0) AS suma_movimientos FROM cuentas_financieras c LEFT JOIN movimientos_financieros m ON m.cuenta_id=c.id WHERE c.tipo='DUENO' GROUP BY c.id,c.saldo ORDER BY c.id;"
```

```powershell
docker compose exec -T postgres psql -U postgres -d promotora -c "SELECT id, cuenta_id, compra_id, monto, fecha FROM movimientos_financieros WHERE tipo='LIQUIDACION' ORDER BY id DESC LIMIT 10;"
```

7. Cada saldo debe coincidir con suma_movimientos. Verificar con JWT de CLIENTE/DUENO que retirar da 403. Para probar cierre, retirar el saldo restante con otra clave y llamar POST cerrar; repetir la liquidación original debe seguir dando 200, mientras una nueva debe dar 409.

## Pruebas automáticas

Docker Desktop operativo. Las pruebas crean bases temporales con migraciones Flyway y credenciales aleatorias en memoria; no usan ni alteran la base local.

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Domain.Tests --no-build -c Release --filter FullyQualifiedName~LiquidacionTests --report-trx
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter FullyQualifiedName~LiquidacionTests --report-trx
```

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter FullyQualifiedName~LiquidacionesApiTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Cobertura: importes/cuentas inválidos, veinte administradores distintos con la misma clave, claves distintas que compiten por saldo, ámbito por cuenta, cambio de monto, cierre y reintento, rollback en ambas auditorías, saldo desconciliado, carrera contra reembolso, permisos/token antiguo y contrato Swagger.

Verificación: **409 pruebas Release aprobadas, 0 fallidas, 0 omitidas**, build sin warnings/errores y formato limpio. 22 casos nuevos: 11 Domain, 8 PostgreSQL y 3 API. Swagger tiene 62 operaciones. La fecha de la primera respuesta se lee después del INSERT para conservar exactamente la precisión de PostgreSQL en los reintentos.
