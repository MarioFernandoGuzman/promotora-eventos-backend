# RabbitMQ y outbox: guía de desarrollo

## Estado del módulo

F6-01 añade el puerto `IMessagePublisher` en Application y el adaptador async en Infrastructure. Worker registra el adaptador cuando se proporciona `RabbitMQ:Host`; exige credenciales externas. La topología se declara al preparar/publicar. Los handlers de negocio deben escribir outbox y nunca invocar este publisher dentro de su transacción.

| Exchange / ruta | Cola durable |
|---|---|
| promotora.compras / compra.procesar | compras.procesar |
| promotora.compras / compra.completada | notificaciones.compra |
| promotora.compras / usuario.registrado | notificaciones.registro |
| promotora.dlx / compra.procesar | compras.procesar.dlq |
| exchange predeterminado / compras.procesar.retry | compras.procesar.retry |

Ambos exchanges son direct y durables; ninguna cola es exclusiva ni auto-delete. La cola de compras envía rechazos sin requeue a DLQ. Retry retorna a `promotora.compras / compra.procesar` mediante dead-letter; el consumidor asignará TTL por mensaje (`Expiration`) según el intento para el backoff (F6-04). Un TTL por cola impediría variar el retraso. ACK manual, prefetch, límite de intentos y procesamiento corresponden a tareas posteriores de consumidores.

Publicación persistente JSON con `MessageId = outbox_mensajes.id`, `Type = tipo` y `mandatory = true`. El adaptador serializa el uso del canal, espera publisher confirms y falla ante retorno sin binding, nack o timeout. Reconecta y redeclara al siguiente intento si el canal está cerrado; no mantiene una cola invisible en memoria.

Paquete nuevo: [RabbitMQ.Client 7.2.2](https://www.nuget.org/packages/RabbitMQ.Client), necesario para el adaptador oficial v7. Se utiliza bajo Apache 2.0, una de sus [dos licencias oficiales](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/LICENSE). [Guía oficial de API async y confirmaciones](https://www.rabbitmq.com/client-libraries/dotnet-api-guide).

## Verificar las pruebas

Requiere Docker operativo. Testcontainers crea RabbitMQ aislado con puertos aleatorios y credenciales efímeras; no modifica las colas de tu Compose. Desde la raíz:

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

Las pruebas verifican routing de los tres tipos, persistencia e identidad, declaración durable repetible, DLQ, TTL y retorno de retry, concurrencia del publisher, cancelación y rechazo de mensajes sin destino. No hay nuevos endpoints ni cambios en Swagger en F6-01.
