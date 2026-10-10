# Revisión del bloque F6-03/F6-04

## Alcance y estado

Rama `feature/f6-compras-consumidor`, basada en `origin/develop` 3e41be4, después del merge del PR #19. Este bloque prepara el POST transaccional con outbox y los componentes de transporte del consumidor. No completa F6-03/F6-04: sus casillas permanecen pendientes hasta integrar pago, ledger, saldos y emisión en F7-02/F7-03. Tampoco cierra F6-08 ni el criterio de salida de Fase 6.

Producción responde 201/200 y conserva compras PENDIENTE. El contrato 202/Location está probado con un puerto de disponibilidad sustituido solo en tests. Activar `Compras:ConsumerEnabled` sin integrador financiero se rechaza al arrancar; no existe implementación de producción que confirme compras sin sus movimientos.

## Evidencia

- `dotnet build --no-restore -c Release -warnaserror`: cero warnings y errores.
- `dotnet test --no-build -c Release --report-trx`: 239 pruebas, cero fallos y cero omisiones (Domain 39, Application 5, Infrastructure 85, API 98, Architecture 12).
- `dotnet format --no-restore --verify-no-changes`: limpio.
- PostgreSQL+pgvector y RabbitMQ reales en Testcontainers: HTTP→outbox→cola, rollback completo, carreras de clave/cupo, contrato 202, duplicados, retry/DLQ, traslado rechazado y cierre durante transacción.
- Toma PENDIENTE→PROCESANDO concurrente: un ganador, una auditoría; rollback revierte toma y deduplicación.

El puerto financiero de pruebas escribe un efecto artificial en una categoría del contenedor temporal. Permite verificar transporte y atomicidad sin afirmar que se probó una compra pagada. La integración financiera necesitará pruebas propias.

## Revisión contra AGENTS.md §§5 y 12

- La reserva sigue siendo UPDATE atómico; reutilizar la clave no reserva ni escribe otro outbox. Compra, detalles, auditoría y outbox comparten transacción.
- La publicación de negocio queda en el relay después del commit. El consumidor confirma individualmente después del éxito comprometido del puerto o de la publicación confirmada del traslado.
- `mensajes_procesados` usa INSERT con ON CONFLICT; requiere transacción explícita con los efectos. La toma usa UPDATE condicional y audita al ganador con IClock UTC.
- Se mantienen propiedad y límites en los handlers; controllers sin EF ni RabbitMQ. Application define puertos, Infrastructure adapta y Worker compone.
- No hay DELETE de datos protegidos, cambios de esquema, migraciones editadas, paquetes nuevos, secretos ni comandos destructivos.
- No se escribe ledger ni se marca COMPLETADA en este bloque: la invariante financiera sigue protegida por la activación pendiente.

## Pendientes y límites

El alcance completo del plan aún exige integración financiera y activación F6-03/F6-04, confirmación/bienvenida F6-05/F6-06, job automático F6-07, pruebas del consumidor financiero F6-08, recuperación F6-11 y los demás criterios de salida pendientes. La emisión y pasarela preparadas en F5 tampoco sustituyen esa integración.

Retry con TTL por mensaje en la cola acordada puede esperar detrás de un mensaje con TTL mayor; no garantiza retraso exacto. Caer entre traslado confirmado y ACK puede duplicar entregas, por eso la deduplicación sigue siendo obligatoria. No se habilita el consumidor con un puerto vacío para eludir pendientes.

Guía de módulos, configuración, Swagger y comandos locales separados: [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).
