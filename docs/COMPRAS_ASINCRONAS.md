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
