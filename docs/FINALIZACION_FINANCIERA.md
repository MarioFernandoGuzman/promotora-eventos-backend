# Finalización financiera — F7-03

## Módulos implementados

Domain/Finance/MovimientosCobro genera VENTA al dueño, COMISION a PLATAFORMA e IMPUESTO a IMPUESTOS desde ImportesCompra congelados. No recibe configuración ni recalcula tasas. Omite componentes cero; una compra gratuita no genera movimientos. Comprueba cuenta activa, dueño y moneda correctos, cuenta única para cada componente y precisión del saldo NUMERIC(12,2). Los importes del plan suman compras.total.

Application/Finance/CompletarCompraService implementa ICompletarCompraPort, un servicio interno al que se entregan compra public_id, ID de intento, respuesta APROBADO y asistentes por detalle. Usa puertos de Finance/Ticketing; no invoca handlers de otro módulo. La pasarela se llama fuera de este servicio y de su transacción.

Infrastructure/Finance/EfCobroRepository bloquea la compra, obtiene el dueño del recinto y bloquea las cuentas por ID ascendente. Usa las cuentas de sistema existentes de V10 y asegura idempotentemente la cuenta GTQ de un dueño anterior que aún no tenga una; nunca reabre CERRADA. Rechaza desbalances previos en las cuentas afectadas, en vez de compensarlos con el nuevo cobro. Inserta el ledger y suma a cada saldo en la misma transacción, sin UPDATE/DELETE de movimientos ni modificación de migraciones.

## Transacción e idempotencia

La operación exige PROCESANDO y un intento correspondiente a la compra. Dentro de una sola transacción:

1. Valida y registra APROBADO mediante el puerto de F7-02.
2. Valida las cuentas y la conciliación previa; inserta los componentes no cero y actualiza saldos. Audita cada movimiento.
3. Cambia PROCESANDO → COMPLETADA mediante UPDATE condicional.
4. Emite boletos mediante IEmisionBoletosPort.EnTransaccionAsync, que comprueba pago, ledger, saldos, categoría y datos de asistentes.
5. Audita el cambio de compra y escribe outbox compra.completada con payload `{ "publicId": "..." }`, sin datos personales ni QR. No publica a RabbitMQ.

Un error esperado devuelve Result y revierte la transacción; una excepción también la revierte. Pago, movimientos, saldos, cuenta del dueño recién creada, boletos, auditorías, estado y outbox quedan juntos. El intento PENDIENTE previamente confirmado se conserva si la finalización falla. No devuelve cupo: la compra sigue PROCESANDO para que el orquestador gestione el resultado y recuperación.

Una repetición sobre COMPLETADA comprueba el intento, respaldo financiero, cantidad completa de boletos y un único outbox de confirmación; devuelve el resultado existente sin otro cobro, movimiento, boleto ni auditoría. Una compra completada con efectos incompletos requiere conciliación y no se repara automáticamente. Los boletos usados conservan su estado. Compras FALLIDA/EXPIRADA/CANCELADA no se finalizan.

## Alcance del PR F7-01 a F7-03

API y Worker registran el componente de finalización cuando existe conexión. El PR financiero proporcionó este componente sin activar el consumidor. La integración posterior F6-04 registra IProcesarCompraMensajePort en Worker y utiliza EnTransaccionAsync para compartir toma/deduplicación y finalización sin transacciones anidadas. El contrato HTTP 202 se activa en F6-03; consultar el estado vigente y la configuración en COMPRAS_ASINCRONAS.md. No se crea un endpoint de pago/finalización independiente ni un flag que permita finalizar sin ledger.

El registro de resultados en pagos sobrevive a nuevos contextos/procesos. FakePaymentGateway sigue conservando su consulta en memoria: falta resolver su respaldo durable antes de habilitar F6-11. Un resultado desconocido tras reinicio no autoriza devolver cupo ni volver a cobrar. La Fase 7 completa sigue pendiente de reembolsos, liquidaciones, consultas y conciliación de escenarios mixtos F7-04 a F7-08.

## Cómo verificar en tu entorno local

Con Docker operativo, desde la raíz, comandos separados:

```powershell
dotnet build --no-restore -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*FinalizacionCompraTests' --report-trx
```

```powershell
dotnet test --project tests/Promotora.Domain.Tests --no-build -c Release --filter-class '*MovimientosCobroTests' --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Estas pruebas usan PostgreSQL+pgvector temporal migrado por Flyway, sin credenciales reales ni cambios en tu base de Compose. Comprueban veinte finalizaciones concurrentes de una compra, diez compras con cuentas compartidas, conciliación, montos congelados D-15, gratuidad, cuentas cerradas, saldo corrupto, resultado ajeno/rechazado, pérdida de estado y rollback por fallos de pago, ledger, boletos, auditoría de compra y outbox. El servicio también se resuelve con DI de Worker sin actor HTTP.

Para las operaciones visibles en Swagger, sigue [CUENTAS_FINANCIERAS.md](CUENTAS_FINANCIERAS.md) y [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). El endpoint nuevo de este bloque es POST /api/v1/cuentas/{id}/cerrar. La finalización se verifica mediante las pruebas anteriores hasta conectar el consumidor; no fuerces estados, pagos ni balances por SQL en tu base local.

Verificación final (2026-10-10): 325 pruebas Release aprobadas, cero fallos/omitidas, build con cero warnings y formato limpio. Revisión del bloque contra AGENTS.md §§5/12: sin incumplimientos detectados dentro de F7-01 a F7-03, sin nuevos paquetes ni cambios de esquema. Los pendientes de activación/recuperación y F7-04 a F7-08 descritos arriba impiden declarar terminadas las fases completas.
