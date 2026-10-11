# Registro de pagos — F7-02

## Módulos y comportamiento

Domain/Finance/Pago valida el resultado contra referencia, monto, moneda y pasarela del intento. Solo permite PENDIENTE → APROBADO/RECHAZADO; repetir el mismo resultado es idempotente, y un resultado contrario no sobrescribe el anterior. Exige fecha UTC. El reembolso corresponde a F7-04.

Application/Finance define IRegistroPagoPort y PagoRegistradoDto. Sus métodos internos IniciarEnTransaccionAsync y ResolverEnTransaccionAsync participan en una transacción explícita del orquestador de compras. No invocan handlers de otro módulo ni cobran por sí mismos.

Infrastructure/Finance implementa el puerto con la tabla pagos ya existente. Bloquea primero la compra con FOR UPDATE para serializar intentos y resultados entre procesos. Un intento nuevo requiere PROCESANDO, toma el total y moneda congelados, usa public_id como referencia y genera pagos.id como identificador de intento. Conserva fecha de creación del intento; la fecha del resultado va en respuesta JSONB.

Reutiliza un intento PENDIENTE o APROBADO compatible sin insertar ni auditar otra vez; APROBADO también puede consultarse por esta operación idempotente si la compra ya está COMPLETADA. No retoma PENDIENTE en una compra FALLIDA ni inicia cobros después de REEMBOLSADO. Tras RECHAZADO, una llamada explícita de inicio puede crear otro intento con ID nuevo, conservando el anterior. La redelivery de un resultado debe usar el mismo ID de intento, no iniciar otro por defecto. No admite resolver un intento si existe otro APROBADO/REEMBOLSADO de esa compra.

La transición usa UPDATE WHERE estado='PENDIENTE'. CREATE del intento y PAYMENT del resultado se auditan con actor null (operación del proceso de compras), entidad pagos e ID de intento; cada auditoría participa en la misma transacción que su operación. Un fallo revierte pago y auditoría. No modifica cupos, ledger, saldos, boletos ni estado de compra.

Solo persiste campos permitidos de RespuestaPago: referencia, monto, moneda, pasarela, estado y fecha. No admite cuerpos crudos de pasarela, tokens, claves ni datos de tarjeta. metodo permanece NULL porque la simulación actual no define un método de pago; no se inventa uno. No se añaden tablas, columnas, paquetes ni migraciones.

## Alcance de integración

No se añade un endpoint de pago ni se activa el consumidor con esta tarea. Swagger conserva el endpoint de cierre de cuentas F7-01; POST /api/v1/compras continúa reservando PENDIENTE hasta conectar el flujo financiero.

F7-03 debe resolver el pago aprobado junto con ledger, saldos, COMPLETADA, emisión y outbox en una misma transacción. El puerto exige la transacción del caller para permitir esa composición sin transacciones anidadas. Es posible confirmar primero el intento PENDIENTE antes de llamar a la pasarela y conservar ese ID para recuperarlo tras un fallo.

Los resultados registrados en PostgreSQL sobreviven a un nuevo contexto/proceso. Esto no vuelve durable el diccionario de FakePaymentGateway: sigue pendiente persistir/consultar la respuesta de la simulación antes de habilitar F6-11. Si el proceso cae entre la respuesta de la pasarela y el registro transaccional, un pago PENDIENTE no prueba rechazo. No devolver cupo ni repetir cobros basándose en un resultado desconocido.

## Cómo probar localmente

Con Docker operativo, desde la raíz del proyecto, comandos separados:

```powershell
dotnet build --no-restore -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*RegistroPagosTests' --report-trx
```

```powershell
dotnet test --project tests/Promotora.Domain.Tests --no-build -c Release --filter-class '*PagoTests' --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Las pruebas usan PostgreSQL+pgvector temporal migrado por Flyway: veinte inicios concurrentes y veinte resultados dejan un pago, una auditoría CREATE y una PAYMENT; rechazos conservados con nuevo intento; resultados incompatibles; transición de compra perdida; gratuidad; rollback de auditoría; exigencia de transacción y persistencia al usar otro contexto. También componen inicio y resultado dentro de una sola transacción del caller y comprueban que un fallo posterior revierta ambos y sus auditorías. No requieren contraseñas reales ni alteran tu base Compose.

Verificación final (2026-10-10): 304 pruebas Release aprobadas, cero fallos/omitidas, build con cero warnings y formato limpio. F7-02 no incorpora nuevas operaciones Swagger; las pruebas HTTP existentes también pasan.
