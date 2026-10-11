# Resiliencia de compras — F6-08

## Qué se comprueba

CompraResilienciaTests ejecuta el transporte de producción ProcesarCompraConsumer y el procesador real ProcesarCompraService con PostgreSQL/pgvector, Flyway y RabbitMQ de Testcontainers. Los adaptadores de prueba observan llamadas o inyectan fallos; no sustituyen pago/ledger/boletos por efectos artificiales.

| Escenario | Comprobaciones |
|---|---|
| 20 mensajes iguales, dos consumidores | Una compra COMPLETADA, una invocación de cobro simulado, un pago APROBADO, tres movimientos, dos boletos y un outbox de confirmación |
| Fallo transitorio después de insertar ledger | Rollback y retry con ID estable; al segundo intento se confirma una sola venta con saldos conciliados |
| Fallo persistente después de insertar ledger | Tres intentos, traslado persistente a DLQ, x-intentos=3; compra PENDIENTE sin pago, ledger, boletos ni deduplicación confirmados |
| Corte de conexión durante emisión, antes de commit | Reentrega marcada Redelivered; rollback de estado/toma, pago, ledger, boletos, auditoría y deduplicación; un consumidor nuevo completa una vez |
| Corte después de commit y antes de ACK | Persisten todos los efectos; se reentrega el ID original y el consumidor nuevo omite sin volver a llamar a la pasarela |

Cada compra reserva dos entradas de Q100 con cupo inicial cinco: subtotal 200, comisión 20, IVA 26.40 y total 246.40. Una venta conserva cupo disponible tres; los tres movimientos suman el total y todos los saldos igualan SUM(ledger). Se validan firmas QR, auditorías y una sola confirmación en outbox.

El mensaje recibido en DLQ contiene exclusivamente publicId. Ninguna prueba usa contraseñas del desarrollador, documentos reales o su base/colas locales.

## Cómo se fuerza la caída

Una pausa controlada ocurre dentro de la emisión transaccional o inmediatamente después del commit del procesador, antes de devolver al transporte. La fixture identifica la conexión del único consumidor en su broker efímero y la interrumpe con la [API HTTP oficial de RabbitMQ](https://www.rabbitmq.com/docs/http-api-reference). No se eliminan colas, mensajes o volúmenes. Después se cancela y dispone el consumidor para liberar su transacción y se arranca otro con scopes y pasarela simulada nuevos.

Esto verifica pérdida real de conexión y reentrega en los dos puntos críticos, no un reinicio del sistema operativo ni un cobro externo. En el escenario posterior al commit, la nueva pasarela recibe cero llamadas porque la deduplicación persistida basta para omitir. En el escenario anterior al commit, la toma también revierte a PENDIENTE. La recuperación de compras PROCESANDO persistidas y la consulta durable del proveedor siguen siendo F6-11.

Las esperas observan commits confirmados y dan tiempo al muestreo de Management; iniciar un callback o ver un contador temporal de cola vacío no demuestra que haya terminado la transacción.

## Ejecutar en tu entorno

Desde la raíz del repositorio, con Docker Desktop operativo:

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests -c Release --filter FullyQualifiedName~CompraResilienciaTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Los contenedores usan puertos aleatorios y credenciales generadas en memoria; no requieren user-secrets ni que ejecutes la API/Worker. Se limpian al terminar. Los reportes TRX se generan en TestResults y no se versionan.

## Módulos y Swagger

No hay módulos ni endpoints nuevos en este bloque. Ticketing, Finance, Audit y el transporte de mensajería son los mismos del PR de integración. En Swagger puedes seguir comprobando POST /api/v1/compras (202 + Location), GET propio por publicId y GET /api/v1/boletos según [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md). Los fallos y cortes de esta guía se reproducen con las pruebas automáticas aisladas, sin interrumpir tu Worker local.

F6-09 amplía la carga a 500–1000 solicitudes: [PRUEBAS_F6_CARGA.md](PRUEBAS_F6_CARGA.md). F6-10 documenta la [topología](TOPOLOGIA_RABBITMQ.md). Suite del bloque: 342 pruebas aprobadas, ninguna fallida/omitida; build sin warnings y formato limpio. F6-11 y la salida completa de Fase 6 siguen pendientes.
