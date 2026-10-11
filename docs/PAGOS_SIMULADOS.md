# Pago simulado — F5-04

Application/Finance define IPaymentGateway, SolicitudPago y RespuestaPago. Infrastructure/Finance implementa FakePaymentGateway y lo registra como singleton en API y Worker. No se contacta una pasarela externa ni se reciben datos de tarjeta. No requiere paquetes, migraciones ni credenciales adicionales.

CobrarAsync recibe la referencia compras.public_id, el total congelado y moneda GTQ. Devuelve Result con APROBADO/RECHAZADO; un rechazo simulado es un resultado normal de la pasarela. Datos inválidos devuelven Validation; reutilizar una referencia con otro monto devuelve Conflict. ConsultarAsync devuelve el resultado conocido o null si la referencia no está registrada en esta instancia.

Cada referencia conserva un único resultado, monto y fecha UTC; llamadas concurrentes comparten ese resultado. La decisión usa una muestra derivada de la referencia, de modo que con igual configuración el estado resulta reproducible. Todo cálculo monetario es decimal. Las compras gratuitas se aprueban, sin implicar movimientos financieros.

## Configuración pública

PagosSimulados:TasaFallo es una fracción entre 0 y 1, inicialmente 0. Tasa 0 aprueba todas las solicitudes válidas; tasa 1 rechaza todos los cobros con monto positivo; 0.25 simula una proporción de rechazo cercana a 25% sobre muchas referencias. No garantiza exactamente esa proporción en una muestra finita. Configuración fuera de rango impide registrar el servicio.

Antes de arrancar API/Worker, en PowerShell:

```powershell
$env:PagosSimulados__TasaFallo = '0.25'
```

```powershell
dotnet run --project src/Promotora.WebApi
```

En otra terminal con su configuración local:

```powershell
dotnet run --project src/Promotora.Worker
```

Cambiar la configuración requiere reiniciar el host. F6-03/F6-04 conectan el cobro simulado en Worker: POST /api/v1/compras devuelve 202 y el cliente consulta su estado por publicId. Pago/ledger/saldos/emisión se confirman juntos; no hay endpoint de pago independiente. Configuración y prueba local en [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).

## Pruebas locales

Con Docker operativo, desde la raíz, ejecutar por separado:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*FakePaymentGatewayTests'
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Se prueban aprobación/rechazo configurable, 20 llamadas concurrentes con una referencia, recuperación del mismo resultado, conflicto de monto sin sobrescritura, precisión/límites/referencia/moneda inválidos, gratuidad, cancelación, DI singleton, configuración inválida y reproducibilidad entre instancias.

## Límite de la simulación y dependencia financiera

La consulta conserva resultados únicamente en memoria y no se comparte entre procesos. Un reinicio pierde esa consulta; null significa desconocido, no confirma rechazo ni ausencia de pago. F7-02 proporciona el registro transaccional de intentos y resultados mediante IRegistroPagoPort; ver [REGISTRO_PAGOS.md](REGISTRO_PAGOS.md). Antes de activar la recuperación F6-11 debe resolverse la consulta durable de referencias de esta simulación; no permitir devolver cupo basándose únicamente en una memoria vacía.

Decisión de integración tomada con el usuario: mantener el orden de fases y preparar los componentes de pago/emisión, sin finalizar una compra HTTP hasta que F7-02/F7-03 proporcionen el registro de pago/ledger requerido por AGENTS.md §5. La base F7-02/F7-03 y la integración F6-03/F6-04 ya están implementadas. La salida completa de Fase 5 permanece pendiente de la prueba de ventas completadas F5-09.
