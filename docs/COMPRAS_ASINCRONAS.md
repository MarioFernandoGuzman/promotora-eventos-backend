# Preparación de compras asíncronas — F6-03/F6-04

## Estado real

POST `/api/v1/compras` ahora confirma juntos reserva, compra PENDIENTE, detalles, auditoría y un mensaje de outbox `compra.procesar`. El payload contiene únicamente `{ "publicId": "UUID de la compra" }`. El relay de F6-02 puede publicarlo a `compras.procesar`; todavía no hay procesamiento financiero habilitado. No representa pago ni venta completada.

Producción sigue respondiendo **201 nueva / 200 repetida**. `ICompraProcesamientoEstado` informa disponibilidad del flujo completo; su registro actual `CompraProcesamientoPendiente` retorna false. No existe configuración para saltar esta protección. El contrato **202 + Location** está preparado en el controller y probado sustituyendo ese puerto únicamente en tests. Cuando se integre F7, PENDIENTE/PROCESANDO devolverán 202 también ante la misma clave; un estado terminal reutilizado devolverá 200. Location apunta a GET `/api/v1/compras/{publicId}`, que siempre comprueba propiedad.

F6-03/F6-04 permanecen pendientes en el plan hasta integrar pago, ledger, saldos y emisión. No se habilita COMPLETADA sin registrar los componentes congelados en la misma transacción. No se modifican las migraciones.

## Módulos y garantías

- Application/Ticketing: CrearCompraHandler escribe IOutboxWriter después de la auditoría, dentro de ITicketingTransaction. Un fallo revierte todas las reservas, detalles, compra, auditoría y outbox.
- Infrastructure: el writer existente persiste JSONB; el registro de disponibilidad mantiene bloqueada la activación. El relay utiliza el ID de outbox como MessageId.
- WebApi: controller traduce el resultado a 201/200 o al contrato preparado 202; no accede a EF ni RabbitMQ.
- Idempotencia: repetir una clave propia devuelve la compra original antes de reservar o escribir; incluso si cambia precio/estado o se cancela, no genera otro mensaje. Clave ajena responde 409 sin revelar compra.

El mensaje solo identifica la compra; el futuro consumidor debe leer los montos congelados y la referencia `compras.public_id` desde PostgreSQL. No confiar en un monto, cliente o cuenta que viniera del mensaje.

## Componentes de consumidor — F6-04

`ProcesarCompraConsumer` consume async con `autoAck=false`, prefetch configurable y un despacho por canal (sin compartir publicaciones concurrentes). Verifica Type, ContentType, MessageId positivo, JSON objeto con publicId UUID no vacío, tamaño máximo 64 KiB y header x-intentos entero no negativo. Copia la memoria del cuerpo dentro del callback de v7. Crea un scope por entrega y llama a `IProcesarCompraMensajePort`; solo Success tras commit permite ACK. Mensajes malformados o Result fallido se trasladan a DLQ; una excepción transitoria permite retry. No registra payload, credenciales ni excepciones sensibles.

Con header x-intentos ausente, el primer intento vale cero. Tras cada fallo se incrementa; con MaxIntentos=3 hay **tres ejecuciones totales**, luego DLQ. Retrasos base de 5 s, 10 s, etc., acotados a 300 s mediante TTL por mensaje en la cola retry, con retorno al exchange de compras. Conserva MessageId, Type, body y headers; elimina Expiration al enviar a DLQ. Espera confirms y mandatory antes de confirmar el original. Caer después del traslado y antes del ACK puede duplicar: la deduplicación sigue siendo necesaria. Si el traslado falla, cierra el canal sin ACK y el broker reentrega. Shutdown cancela el puerto, revierte su transacción y deja el original sin confirmar.

`EfMensajeProcesadoRepository.RegistrarAsync` utiliza INSERT parametrizado con ON CONFLICT DO NOTHING sobre (mensaje_id, consumidor) y fecha IClock UTC. Requiere una transacción activa: false permite ACK/omitir; si la transacción falla, el registro desaparece junto con los efectos. No registra un mensaje por anticipado fuera de los efectos del negocio.

`EfCompraTomaRepository.TomarAsync` exige transacción activa, realiza UPDATE PENDIENTE→PROCESANDO por publicId con procesando_desde UTC y audita al ganador en esa transacción. Cero filas devuelve null sin leer montos ni auditar. El ganador obtiene los componentes congelados desde PostgreSQL. No cobra, finaliza, devuelve cupo ni emite boletos. El integrador de F7 deberá coordinar toma, referencia idempotente, deduplicación, pago/ledger/saldos/emisión y compra.completada; además probar reinicios y recuperación de F6-11.

`ProcesarCompraConsumerService` administra reconexión del transporte en Worker, con espera cancellable de 2 s. El registro `AddCompraConsumer` exige que exista el puerto de integración financiera antes de activar el transporte. Ese puerto **no tiene implementación registrada de producción todavía**. El adaptador de tests escribe una categoría como efecto artificial, únicamente en PostgreSQL temporal; no simula una compra pagada ni sustituye las pruebas financieras de F7.

| Configuración de Worker | Valor y estado |
|---|---|
| Compras:ConsumerEnabled | false; true se rechaza al arrancar hasta integrar F7 |
| Compras:Prefetch | 16; rango 1–1000 |
| Compras:MaxIntentos | 3 ejecuciones totales; rango 1–10 |
| Compras:RetryBaseDelayMilliseconds | 5000; rango 1–300000 |

No habilites el consumidor para probar compras pagadas en este bloque: la protección rechaza esa configuración y las compras permanecen pendientes. La prueba automática sí conecta el transporte real a un puerto explícito de pruebas y comprueba efectos/dedup atómicos, retry/DLQ, toma concurrente, traslado fallido y shutdown.

TTL por mensaje en una sola cola puede sufrir espera detrás de mensajes anteriores con TTL mayor; no se promete un tiempo exacto de retry. Esta decisión conserva la topología acordada y no crea colas adicionales. [Semántica oficial de TTL](https://www.rabbitmq.com/docs/ttl) y [memoria/consumo async v7](https://www.rabbitmq.com/client-libraries/dotnet-api-guide).

## Verificar localmente

Configurar API/Swagger según [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md) y Worker/Management según [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). Desde la raíz, con Docker operativo:

```powershell
dotnet restore
```

```powershell
dotnet build --no-restore -c Release -warnaserror
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

```powershell
dotnet run --project src/Promotora.WebApi
```

En Swagger autenticado como CLIENTE, crear una compra de un evento EN_VENTA con `Idempotency-Key` nueva. Resultado actual 201 PENDIENTE; repetir con la misma clave da 200 y el mismo publicId. Consultar por publicId y cancelar conserva las reglas previas. No hay usuario/contraseña nuevos: utiliza tus credenciales configuradas fuera del repo.

Si ejecutas el relay, el mensaje aparecerá en `compras.procesar` y permanecerá sin consumir hasta que el flujo financiero esté listo. No esperes COMPLETADA en este bloque. La suite verifica HTTP→outbox→RabbitMQ, rollback tras escritura del outbox, contrato 202/Location preparado y un solo mensaje en las carreras de idempotencia/cupo.
