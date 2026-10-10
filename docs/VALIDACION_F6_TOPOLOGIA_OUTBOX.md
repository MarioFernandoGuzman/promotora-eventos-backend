# Revisión F6-01 / F6-02

Rama `feature/f6-topologia-outbox`, desde `origin/develop` dde5a41 (PR anterior integrado). Dos tareas en orden y un commit por tarea, sin push ni merge.

## Qué se implementó

Application/Shared define contratos neutrales: mensaje publicable, tipos admitidos, writer, publisher y relay. Infrastructure/Shared implementa RabbitMQ async v7, topología durable y persistencia de outbox sobre V8 existente. Worker activa el servicio periódico con configuración externa y crea un scope por ciclo. No se modificaron migraciones, endpoints, cupos, compras, pagos ni ledger.

El writer comparte la transacción del negocio; el relay solo lee filas ya confirmadas, usa un contexto independiente y `FOR UPDATE SKIP LOCKED`. Publicación persistente y mandatory con confirms antes de marcar publicado. Cada mensaje usa su propia transacción de relay; un fallo posterior no revierte confirmados anteriores. No hay DELETE ni pérdida de mensajes por límite de reintentos de transporte. Repetir tras caída conserva el ID.

## Evidencia

- `dotnet build --no-restore -c Release -warnaserror`: cero warnings y errores.
- `dotnet test --no-build -c Release --report-trx`: **228 pasadas, 0 fallidas, 0 omitidas** (Domain 39, Application 5, Infrastructure 77, API 95, Architecture 12).
- `dotnet format --no-restore --verify-no-changes`: limpio.
- `git diff --check`: limpio.
- F6-01: cinco pruebas RabbitMQ real: routing/persistencia/durabilidad, TTL→retorno y DLQ, canal concurrente, NO_ROUTE y validación/cancelación.
- F6-02: siete pruebas con PostgreSQL+pgvector/Flyway/rol restringido y RabbitMQ real: commit/rollback conjunto, fallo/reintento con identidad, SKIP LOCKED entre dos relays, caída tras confirms sin commit y republicación con el mismo ID, NO_ROUTE conserva pendiente, rechazo fuera de transacción y host Worker periódico con parada.

Se corrigió la espera de arranque del contenedor para evitar ejecutar rabbitmq-diagnostics antes de que el servidor prepare el archivo de Erlang. Se usa la señal del log de arranque. Tests aislados sin credenciales ni puertos locales; las contraseñas se generan en memoria.

## Revisión contra AGENTS.md §§5 y 12

Sin incumplimientos detectados dentro de F6-01/F6-02: puertos en Application, adaptadores en Infrastructure, composición en Worker; dependencias verificadas. Outbox dentro del commit de negocio y publisher separado. Estado `publicado` se actualiza condicionalmente. UTC mediante IClock y CT en operaciones async; logs del nuevo servicio sin contenido sensible. Solo nuevo paquete RabbitMQ.Client 7.2.2, justificado y con licencia Apache 2.0 verificada en [DEPENDENCIAS.md](DEPENDENCIAS.md).

## Pendientes y límites del bloque

No se cumple aún la salida de Fase 6: faltan F6-03–F6-11 y el ledger de F7. F5-09 y la salida de Fase 5 siguen pendientes de ventas completas. POST conserva su contrato actual y todavía no escribe outbox. No hay cobro, emisión, consumidores ni correo activados por este PR. No se marca anticipadamente ninguna de esas tareas.

Publisher confirms acredita recepción en el broker, no procesamiento del negocio. Entrega al menos una vez: el consumidor debe deduplicar en su transacción mediante mensajes_procesados. Retry con TTL por mensaje tiene retorno configurado, pero el backoff/límite de intentos se conectará en F6-04. El relay detiene el lote ante error y conserva el mensaje; un mensaje inválido antiguo requiere diagnóstico para no bloquear los lotes siguientes. La inicialización/reconexión se reintenta en cada ciclo y las credenciales deben corresponder al broker existente.

Configuración, usuario de RabbitMQ, pruebas y guía frontend/Swagger: [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). No se crean credenciales de API ni se revelan secretos en este bloque.
