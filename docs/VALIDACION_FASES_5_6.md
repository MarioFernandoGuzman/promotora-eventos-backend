# Revisión de salida — Fases 5 y 6

## Resultado y alcance

F5-01–F5-10 y F6-01–F6-11 completas localmente, incluida la base financiera F7-01–F7-03 adelantada con autorización del usuario. F5-09 se cierra con las ventas completas concurrentes de F6-09, repetidas con el diario durable F6-11. No hay saltos hacia F7-04: reembolsos y liquidaciones siguen pendientes.

Verificación del 11 de octubre de 2026: `dotnet build -c Release -warnaserror`, cero warnings/errores; `dotnet test --no-build -c Release --report-trx`, **360 aprobadas, 0 fallidas, 0 omitidas**; `dotnet format --no-restore --verify-no-changes`, limpio. Los cambios posteriores de cierre son documentación y casillas, sin cambios de código. Los contenedores temporales migran con Flyway; CI remoto pendiente de publicar el PR.

## Criterios de salida

| Criterio | Evidencia |
|---|---|
| Compra completa de punta a punta | CompraOutboxApiTests: HTTP 202/Location → outbox → RabbitMQ → pago simulado → ledger/saldos/boletos → polling propio COMPLETADA |
| Cero sobreventa | CompraConcurrenciaApiTests: cantidades 1/2/3; CompraCargaApiTests: 500/1000 solicitudes, cupo 100, exactamente 100 ventas completas por caso |
| Idempotencia HTTP y del consumidor | CompraIdempotenciaApiTests, ProcesarCompraTests y CompraResilienciaTests: claves repetidas, 20 duplicados/dos consumidores, un solo efecto confirmado |
| Fallos, retry, DLQ y reentrega | CompraResilienciaTests: rollback tras ledger, tres intentos a DLQ y corte AMQP antes/después del commit; proveedor durable en la suite actual |
| Recuperación PROCESANDO | RecuperacionComprasTests: aprobación/rechazo/ausencia, dos recuperadores, consulta fallida, pago histórico inconsistente, rollback y host periódico |
| Carreras y devolución única | Seis carreras procesar/expirar/cancelar/recuperar desde PENDIENTE o PROCESANDO; cupo final 3 por venta o 5 por reserva devuelta de un cupo inicial 5, nunca devolución duplicada |
| Evidencia de mensajería y correo | OutboxTests, CompraOutboxApiTests, ConfirmacionNotificacionTests y BienvenidaNotificacionTests; PostgreSQL, RabbitMQ y MailHog reales |
| Diagrama y operación local | TOPOLOGIA_RABBITMQ.md, COMPRAS_ASINCRONAS.md y RECUPERACION_COMPRAS.md |

## Revisión del diff frente a AGENTS.md §§5/12

- Reserva y devolución siguen siendo UPDATE condicionales; el ganador modifica cupo en su transacción. La recuperación revalida estado/timeout y omite compras bloqueadas.
- Pago, ledger, saldos, COMPLETADA, boletos, auditoría y outbox se confirman juntos. El diario del proveedor simulado se confirma independientemente y no sustituye el ledger.
- Deduplicación se confirma con efectos. Una entrega normal que pierde la toma no guarda un marcador nuevo capaz de bloquear PROCESANDO. Un recuperador omitido revierte su marcador provisional.
- No se publica a RabbitMQ dentro de transacciones de negocio; ACK ocurre después del commit o confirms del traslado. Se conserva el ID del outbox.
- Sin nuevos endpoints ni acceso EF/MQ en controllers; puertos en Application y adaptadores en Infrastructure. Reglas de arquitectura aprobadas.
- Una migración nueva V11, aprobada por el usuario; ninguna migración anterior editada. Diario INSERT-only, sin FK intencional, permisos restringidos y trigger de inmutabilidad. Sin cambios del esquema bi ni permisos operativos para bi_reader.
- Diccionario y ER actualizados: 27 tablas, 203 columnas. Readiness obtiene conteos del modelo y verifica permisos del diario. Fechas UTC/IClock, decimal para dinero y CancellationToken en operaciones async.
- Sin paquetes nuevos, secretos versionados, borrados financieros ni comandos destructivos sobre el entorno local. Commits locales en español con Refs; sin push/merge del agente.

**Incumplimientos detectados en el diff: ninguno dentro del alcance simulado aprobado.**

## Límites que permanecen

El consumidor simulado conserva toma/finalización en una transacción; PROCESANDO normalmente revierte a PENDIENTE ante un fallo. El job recupera PROCESANDO persistidas, preparadas explícitamente en pruebas. Un diario aprobado sin efectos de compra no representa una venta ni un cobro externo. Una pasarela real necesita otro protocolo de toma durable, cobro fuera de la transacción y conciliación de estados; no está habilitada.

La recuperación necesita el outbox original para validar identidad/asistentes. Outbox inválido, marcador histórico sin efectos o respaldo financiero contradictorio requieren conciliación; no se fuerzan estados ni se devuelven cupos automáticamente. Un error de consulta tampoco significa ausencia de pago. No hay endpoint administrativo de reparación en el plan actual.

SMTP y PostgreSQL no tienen commit distribuido: puede repetirse correo ante caída entre aceptación SMTP y commit, con Message-ID estable. Los asistentes permanecen en outbox tras publicar; purga automática aún no definida. Las cifras de carga son locales con TestServer, no un SLA de producción. IVA/comisión siguen D-15; facturación electrónica y reembolsos parciales fuera de alcance.

## Entrega y prueba local

La rama `feature/f6-recuperacion-compras` parte de develop actualizado después del PR #25. Este bloque entrega F6-11 y el cierre documental F5-09, una tarea por commit. El usuario hace push/PR y verifica CI antes de integrar.

Guías:

- [Recuperación y V11](RECUPERACION_COMPRAS.md): migrar, activar Worker y repetir las pruebas aisladas.
- [Compras asíncronas](COMPRAS_ASINCRONAS.md): API 202, polling, boletos y configuración externa.
- [Swagger autenticado para frontend](FRONTEND_SWAGGER_AUTENTICACION.md): usuarios actuales, login y Authorize.
- [Carga](PRUEBAS_F6_CARGA.md) y [resiliencia](PRUEBAS_F6_RESILIENCIA.md): escenarios, evidencia y comandos separados.
