# Revisión de expiración y pruebas de Fase 5

Base: origin/develop 5c453da (PR #17 integrado). Rama: feature/f5-expiracion-pruebas. Commits separados con Refs F5-08, F5-09 y F5-10.

## Cambios y validación

F5-08: handler interno por lotes, puerto/consulta EF y DI independiente de actor HTTP; ejecución periódica pendiente de F6-07. F5-09: evidencia de reservas HTTP concurrentes con ocho clientes distintos, cantidades exactas y rechazos sin compras parciales. Su casilla permanece pendiente de ventas completadas. F5-10: idempotencia HTTP de una compra por clave, disputa entre clientes y reintentos tras cancelación/falta de cupo.

- `dotnet build --no-restore -warnaserror -c Release`: cero advertencias/errores.
- `dotnet test --no-build -c Release --report-trx`: 216 aprobadas, cero fallos/omisiones: 39 Domain, 5 Application, 65 Infrastructure, 95 API, 12 Architecture. Antes de cada commit: 209 para F5-08, 212 con la evidencia de F5-09, 216 con F5-10.
- `dotnet format --verify-no-changes --no-restore`: limpio.
- `git diff --check`: sin errores.
- Swagger conserva 59 operaciones; ningún endpoint, migración o paquete nuevo. Pruebas contra PostgreSQL real temporal con Flyway/rol restringido; no modifican Compose.

## AGENTS.md §§5 y 12

Expiración solo de PENDIENTE vencidas, con UPDATE condicional que revalida estado/fecha después de consultar candidatas. Solo el ganador devuelve cupo y audita, en la misma transacción. Diez handlers simultáneos no duplican efectos. Cancelar/tomar para PROCESANDO entre consulta y UPDATE impide otra devolución. Un fallo de auditoría revierte todo el lote.

La concurrencia comprueba cupo final cero y detalle total exactamente igual a K en los tres casos, con clientes distintos para ejercitar el inventario compartido. La idempotencia comprueba una sola compra/detalle/auditoría CREATE/reserva; un 409 previo no consume la clave y repetir una compra cancelada no reserva otra vez. El cliente perdedor no recibe datos de una compra ajena.

No hay borrados, publicaciones RabbitMQ dentro de transacciones, modificaciones de ledger/saldos/pagos ni cambios de migraciones existentes/bi. Application solo conoce puertos/Domain; controllers sin cambios; Worker solo registra DI, sin scheduler añadido anticipadamente. UTC con IClock, async con CancellationToken, sin Sleep/Wait bloqueante ni secretos versionados.

No se detectaron incumplimientos en el alcance implementado. Las limitaciones siguientes son explícitas y conservan la dependencia financiera ya acordada; no se declara cerrada la fase.

## Criterio de salida pendiente

La salida exige compra completa de punta a punta sin sobreventa. Todavía no se cumple:

1. POST /compras reserva PENDIENTE; pago, ledger y boletos deben conectarse en la misma transacción mediante F7-02/F7-03. No se fuerza COMPLETADA en producción sin ese respaldo.
2. F5-09 permanece sin marcar: pruebas de reservas listas, pero falta comprobar compras COMPLETADA/pagos/ledger/boletos bajo concurrencia al integrar F7.
3. La ejecución periódica de expiración/coordinación del Worker corresponde a F6-07. El handler actual no se dispara automáticamente al arrancar.
4. La pasarela simulada aún guarda resultados en memoria por proceso; se requiere consulta durable antes de activar recuperación F6-11, como ya advierte el plan. Desconocido no significa ausencia de pago.

F5-08/F5-10 se marcan en sus respectivos commits; ninguna tarea de F6/F7 se marca anticipadamente. La facturación electrónica y los reembolsos parciales continúan fuera de alcance.

Guías: [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md), [PRUEBAS_F5_CONCURRENCIA.md](PRUEBAS_F5_CONCURRENCIA.md), [PRUEBAS_F5_IDEMPOTENCIA.md](PRUEBAS_F5_IDEMPOTENCIA.md) y [COMPRAS_API.md](COMPRAS_API.md).
