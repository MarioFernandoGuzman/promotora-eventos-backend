# RabbitMQ y outbox: guía de desarrollo

## Estado del módulo

F6-01 añade el puerto `IMessagePublisher` en Application y el adaptador async en Infrastructure. Worker registra el adaptador cuando se proporciona `RabbitMQ:Host`; exige credenciales externas. La topología se declara al preparar/publicar. Los handlers de negocio deben escribir outbox y nunca invocar este publisher dentro de su transacción. F6-03/F6-04 ya integran compra 202 y procesamiento financiero; diagrama y semántica actual en [TOPOLOGIA_RABBITMQ.md](TOPOLOGIA_RABBITMQ.md).

| Exchange / ruta | Cola durable |
|---|---|
| promotora.compras / compra.procesar | compras.procesar |
| promotora.compras / compra.completada | notificaciones.compra |
| promotora.compras / usuario.registrado | notificaciones.registro |
| promotora.dlx / compra.procesar | compras.procesar.dlq |
| exchange predeterminado / compras.procesar.retry | compras.procesar.retry |

Ambos exchanges son direct y durables; ninguna cola es exclusiva ni auto-delete. La cola de compras envía rechazos sin requeue a DLQ. Retry retorna a `promotora.compras / compra.procesar` mediante dead-letter; el consumidor asigna TTL por mensaje (`Expiration`) según el intento para el backoff (F6-04). Un TTL por cola impediría variar el retraso. El consumidor financiero usa ACK manual después del commit, prefetch configurable y tres ejecuciones por defecto antes de DLQ.

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

Las pruebas verifican routing de los tres tipos, persistencia e identidad, declaración durable repetible, DLQ, TTL y retorno de retry, concurrencia del publisher, cancelación y rechazo de mensajes sin destino. No hay nuevos endpoints ni cambios en Swagger en F6-01/F6-02.

## Escritura y relay — F6-02

`IOutboxWriter` es scoped y comparte el DbContext de `IUnitOfWork`. Exige una transacción explícita activa, valida agregado/ID/tipo/objeto JSON y guarda la fila junto con los cambios pendientes del negocio, sin hacer commit ni acceder a RabbitMQ. Si el caso de uso falla, ambos se revierten. Devuelve el ID estable de V8; el puerto recibe un `JsonElement`, sin dependencias EF o RabbitMQ en Application. CrearCompraHandler ya escribe outbox como preparación de F6-03. Identidad guarda `usuario.registrado`; bienvenida y confirmación están implementadas en F6-05/F6-06 y se habilitan con configuración adicional de [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md). Activación financiera y contrato HTTP: [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).

`IOutboxRelay` usa un contexto independiente, sin cambios pendientes ni transacción de negocio. Selecciona por ID con `FOR UPDATE SKIP LOCKED`, una fila y una transacción de relay por mensaje; las filas del negocio ya están confirmadas antes de ser visibles. Mantiene el lock hasta confirmar el envío y guardar `publicado=true`, `fecha_publicado` UTC e incrementar `intentos` con UPDATE condicional `WHERE publicado=false`. Dos instancias saltan filas bloqueadas. Una cancelación o fallo SQL revierte la transacción del relay y libera el lock al disponerla.

Ante error del publisher incrementa intentos, mantiene pendiente y detiene ese lote; se reintenta en el siguiente ciclo sin eliminar filas. Un mensaje inválido antiguo puede detener cada lote: revisar tipo/bindings y logs de broker antes de reintentar, sin marcarlo publicado manualmente. El máximo de tres intentos y la DLQ corresponden al consumidor, no al relay: el relay no descarta mensajes por fallos del transporte.

La entrega es **al menos una vez**. Si RabbitMQ recibió el mensaje y el proceso cae antes del commit, el siguiente relay lo republica con el mismo `MessageId`. F6-04 registra `mensajes_procesados(mensaje_id, consumidor)` con los efectos en una sola transacción. Una confirmación del broker no significa que una compra ya fue procesada. COMPLETADA exige pago aprobado, ledger y saldos de F7-02/F7-03 junto con boletos y auditoría.

`OutboxRelayService` prepara la topología al iniciar cada ciclo, crea un scope, procesa un lote y espera con cancelación. Logs solo con conteos/estado, sin payload, credenciales o excepciones sensibles. Shutdown y conexiones se disponen de forma async. Por defecto está deshabilitado; para habilitarlo requiere conexión PostgreSQL y configuración RabbitMQ completas.

| Clave externa | Valor / regla |
|---|---|
| RabbitMQ:Host | localhost para Compose; requerido si habilitas outbox |
| RabbitMQ:Port | 5672 por defecto |
| RabbitMQ:VirtualHost | / por defecto |
| RabbitMQ:Username | promotora para el Compose existente |
| RabbitMQ:Password | contraseña actual del broker; sin valor predeterminado |
| RabbitMQ:PublishTimeoutSeconds | 15 por defecto; 1–120 |
| ConnectionStrings:Promotora | login restringido de PostgreSQL; sin valor predeterminado |
| Outbox:Enabled | false por defecto; true para iniciar el relay |
| Outbox:BatchSize | 50 por defecto; 1–1000 |
| Outbox:PollIntervalSeconds | 2 por defecto; 1–300 |

## Ejecutar Worker en tu entorno local

Desde la raíz, con Docker y la base migrada según [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md). Los comandos de lectura de contraseñas funcionan en Windows PowerShell 5.1 y 7. Utiliza las contraseñas actuales de tus volúmenes; cambiar una variable no cambia credenciales de un broker/base ya inicializados.

Si las variables de Compose no están en esta terminal, leerlas sin imprimirlas:

```powershell
$env:POSTGRES_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Contraseña actual de postgres para Compose' -AsSecureString)).Password
```

```powershell
$env:RABBITMQ_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Contraseña actual de RabbitMQ para Compose' -AsSecureString)).Password
```

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
docker compose run --rm flyway info
```

Configurar el proceso Worker por variables de esta terminal:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
```

```powershell
$env:RabbitMQ__Host = 'localhost'
```

```powershell
$env:RabbitMQ__Username = 'promotora'
```

```powershell
$env:RabbitMQ__Password = $env:RABBITMQ_PASSWORD
```

Si `ConnectionStrings__Promotora` ya existe en la terminal, se comparte con Worker. Si solo está en user-secrets de WebApi, configura también la conexión restringida del Worker; este host tiene un almacén independiente. El login debe existir previamente según la guía local:

```powershell
$workerConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
```

```powershell
$workerConnection['Host'] = 'localhost'
```

```powershell
$workerConnection['Port'] = 5432
```

```powershell
$workerConnection['Database'] = 'promotora'
```

```powershell
$workerConnection['Username'] = 'promotora_runtime'
```

```powershell
$workerConnection['Password'] = [System.Net.NetworkCredential]::new('', (Read-Host 'Contraseña actual de promotora_runtime' -AsSecureString)).Password
```

```powershell
$env:ConnectionStrings__Promotora = $workerConnection.ConnectionString
```

```powershell
Remove-Variable workerConnection
```

```powershell
$env:Outbox__Enabled = 'true'
```

```powershell
dotnet run --project src/Promotora.Worker
```

Opcionalmente guardar los mismos valores con `dotnet user-secrets set` para Worker, en Development; nunca en appsettings ni archivos del repo. Las claves de boletos configuradas previamente se conservan; este bloque no necesita un usuario nuevo de la API ni contraseña de bootstrap.

Abrir [RabbitMQ Management](http://localhost:15672): usuario `promotora`, contraseña actual leída arriba. Deben aparecer dos exchanges (`promotora.compras`, `promotora.dlx`) y las cinco colas de la tabla. Con outbox vacío no se generarán mensajes. Crear una compra en Swagger reserva PENDIENTE y alimenta outbox; para completar el flujo habilita también `Compras:ConsumerEnabled` con firma QR y categoría externa según [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md). Para verificar escritura, rollback, confirmación y reentrega usa la suite automática, sin insertar SQL manual en tu base local. Recuperación durable de PROCESANDO y activación opt-in en [RECUPERACION_COMPRAS.md](RECUPERACION_COMPRAS.md).

Las pruebas `OutboxTests` usan PostgreSQL+pgvector migrado con Flyway y el rol restringido, junto con RabbitMQ real: negocio/outbox rollback, rechazo fuera de transacción, fallo y reintento, mensaje sin destino, dos relays con fila bloqueada, caída tras confirmación antes de commit con ID repetido y host periódico con shutdown. No hay pruebas omitidas ni dependencias de tus puertos/contraseñas locales.

Swagger y la autenticación de frontend siguen documentados en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md); los módulos internos de mensajería se verifican en Worker, RabbitMQ y pruebas, sin endpoints de prueba.
