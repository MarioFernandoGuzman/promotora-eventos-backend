# Correos y notificaciones — F6-05

## Módulos y estado

Application define IEmailSender, los contratos de notificación y EnviarConfirmacionHandler. Infrastructure implementa SMTP, consultas del esquema existente y EnviarConfirmacionConsumer. Worker registra el consumidor solamente si Notificaciones:Enabled=true; exige PostgreSQL, RabbitMQ y SMTP configurados. No hay endpoints nuevos ni cambios en migraciones.

El consumidor escucha notificaciones.compra / compra.completada. Contrato: Type=compra.completada, ContentType=application/json, MessageId=ID positivo del outbox, payload `{ "publicId": "UUID" }`. Copia el cuerpo durante el callback de RabbitMQ v7, límite 64 KiB, prefetch=1 y ACK manual individual después del commit. Cada entrega crea un scope.

El handler inserta mensajes_procesados(mensaje_id, EnviarConfirmacionConsumer) en la transacción de notificación. Un duplicado confirmado se omite sin correo ni otra notificación; entregas concurrentes se serializan por esa clave. Consulta la compra desde la BD, con bloqueo, y exige COMPLETADA, respaldo financiero consistente, boletos emitidos en cantidad exacta y cliente activo. Nunca confía en destinatarios, montos ni QR provenientes del evento.

El correo de texto contiene los identificadores y códigos de los boletos. El código QR es el token para presentar en puerta; no se genera una imagen QR ni un PDF en esta tarea. El mensaje privado solo pasa a SMTP: notificaciones.mensaje guarda una descripción genérica, sin cuerpo, tokens ni datos del proveedor. ENVIADO significa aceptado por el servidor SMTP, no entregado al buzón final.

La emisión de compra.completada desde el finalizador sigue pendiente de F7. El consumidor está implementado y probado con compras existentes preparadas en contenedores; el flujo local actual de compra permanece PENDIENTE y no envía confirmación. No actives Compras:ConsumerEnabled para eludir esa dependencia. F6-06, bienvenida, será el siguiente commit.

## Fallos e idempotencia

Hasta tres intentos SMTP por entrega, con espera cancellable de 2 s por defecto y timeout SMTP de 15 s por intento. Si todos fallan se registra FALLIDO y se confirma el mensaje después del commit; no hay reenvío automático ilimitado ni una nueva cola fuera de la topología. Mensaje con ID válido pero JSON/compra no válidos registra FALLIDO sin enviar. Un MessageId ausente o inválido no puede deduplicarse: cierra el canal sin ACK y requiere revisar el productor; se conserva en la cola y puede bloquearla.

Errores inesperados de BD y cancelación revierten deduplicación/notificación; al cerrarse el canal RabbitMQ reentrega. El host reconecta con espera cancellable de 2 s y registra solo una advertencia genérica. No se publican mensajes dentro de esta transacción.

SMTP y PostgreSQL no tienen commit distribuido: caer después de aceptación SMTP y antes del commit puede repetir el correo. Message-ID es estable por evento para facilitar identificación; no garantiza que el servidor deduplique. Las pruebas garantizan un correo para duplicados ya confirmados y concurrentes, no entrega exactamente una vez ante esa caída. Reenvío administrativo de FALLIDO queda fuera de estos endpoints/tareas.

## Configuración y prueba local

Desde la raíz del repo, configurar primero las conexiones del Worker y arrancar Compose según [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). En esa misma terminal, comandos separados:

```powershell
$env:Smtp__Host = 'localhost'
```

```powershell
$env:Smtp__Port = '1025'
```

```powershell
$env:Smtp__From = 'promotora@example.invalid'
```

```powershell
$env:Notificaciones__Enabled = 'true'
```

```powershell
dotnet run --project src/Promotora.Worker
```

MailHog local no requiere usuario/contraseña SMTP. Fuera de desarrollo, configurar Smtp:EnableSsl=true y proporcionar Smtp:Username/Smtp:Password por variables externas o user-secrets; se rechaza autenticación sin TLS. No copiar credenciales al repo. MaxIntentos admite 1–10, RetryDelayMilliseconds 1–30000 y TimeoutSeconds 1–120.

Abrir [MailHog](http://localhost:8025) y [RabbitMQ Management](http://localhost:15672). Por ahora no se producirá confirmación desde Swagger: validar F6-05 con las pruebas automáticas aisladas, sin fabricar ventas ni modificar estados en tu BD local. Autenticación Swagger sigue en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md).

```powershell
dotnet build --no-restore -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*ConfirmacionNotificacionTests' --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

No paquetes nuevos: System.Net.Mail del runtime para el SMTP sencillo de MailHog, sin OAuth ni proveedor externo. [API oficial async con CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.sendmailasync?view=net-10.0). La prueba real verifica también el cuerpo MIME UTF-8/Base64 recibido, destinatario, identidad del correo y boletos.
