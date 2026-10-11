# Compras asíncronas — F6-03/F6-04

## Flujo implementado

La API reserva cupo y confirma compra PENDIENTE, detalles, auditoría y outbox juntos. El relay publica después del commit. RabbitMQ recibe únicamente `{ "publicId": "UUID" }`, con MessageId igual al ID del outbox; los montos se leen de PostgreSQL.

Los ítems admiten `asistentes`: una persona por unidad cuando se proporciona, obligatorio en carreras. Cada persona tiene `nombreAsistente`, `documentoIdentidad` y `datosExtraJson` opcional (string JSON válido). Los datos se validan antes de reservar; no se aceptan nombres/documentos vacíos en carreras. Si se repite una clave propia, se conserva la solicitud original, incluyendo asistentes. Los ítems del mismo tipo se agrupan conservando el orden de las personas.

Los asistentes se guardan por tipoEntradaId en outbox_mensajes.payload, y se relacionan con detalle_compra al emitir. El relay los elimina del cuerpo enviado al broker. No se incluyen en RabbitMQ, DLQ, logs ni respuestas de compra. Persisten en el outbox después de publicar y también se copian a boletos al completar. Todavía no existe purga automática del outbox; el equipo debe definir esa política antes de producción.

## Procesamiento y transacción

Application/Ticketing/ProcesarCompraService implementa IProcesarCompraMensajePort mediante puertos. Comprueba que el ID y publicId recibidos coinciden con un outbox de compra existente; un mensaje arbitrario no permite cobrar otra compra.

Una única transacción engloba deduplicación, UPDATE PENDIENTE→PROCESANDO con fecha UTC, intento de pago, resultado, ledger/saldos, COMPLETADA, boletos, auditorías y outbox compra.completada. Los duplicados confirmados y compras que pierden la toma se omiten. Solo se envía ACK después del commit.

El pago RECHAZADO registra su resultado, pasa condicionalmente PROCESANDO→FALLIDA y devuelve cupo una vez, todo junto con la deduplicación. No genera ledger ni boletos. Ante un error esperado se revierte la transacción y el transporte lleva el mensaje a DLQ; la reserva sigue PENDIENTE y puede expirar/cancelarse. Una excepción reintenta con backoff; shutdown revierte y deja el mensaje sin ACK. Nunca se publica directamente desde esta transacción.

**Alcance de la pasarela:** el adaptador del proyecto es SIMULADA, sin cobros externos. Su llamada ocurre dentro de la transacción del consumidor para que un fallo revierta también la toma y permita reentrega. Mantiene el resultado por publicId en memoria durante la vida del Worker. No sustituirla por una pasarela externa con este mismo protocolo: necesita recuperación y conciliación durable. F6-11 sigue pendiente; no se habilita el job de recuperación ni se interpreta un resultado desconocido como rechazo. PROCESANDO normalmente no es visible a otras conexiones porque se confirma junto con el resultado final.

## Transporte y módulos

Infrastructure valida Type, ContentType, MessageId, publicId, tamaño y x-intentos. Despacho serial por canal, prefetch configurable, mensajes persistentes, publisher confirms y ACK manual. Una excepción usa TTL exponencial acotado; tres ejecuciones totales por defecto, luego DLQ. Result fallido y mensaje malformado van a DLQ. Si falla el traslado, se cierra el canal sin ACK. Nunca registrar cuerpos, QR ni excepciones con configuración sensible.

Worker registra el procesador real con AddProcesamientoCompras. AddCompraConsumerComponents conserva los repositorios base para pruebas de transporte. API/Worker conocen Infrastructure solamente para DI; los controllers siguen llamando handlers. La finalización usa ICompletarCompraPort.EnTransaccionAsync para no crear una transacción anidada.

## Preparar el entorno local

Primero configura la conexión y JWT de la API según [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). Configura RabbitMQ y la conexión del Worker según [MENSAJERIA_OUTBOX.md](MENSAJERIA_OUTBOX.md). Las contraseñas existentes permanecen en user-secrets/variables; no hay un usuario o contraseña nuevo en este bloque.

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

Consulta el ID real de Carrera con una conexión de tu entorno (no asumir el ID de pruebas):

```sql
SELECT id, nombre FROM categorias_evento WHERE nombre = 'Carrera';
```

```powershell
$categoriaCarreraId = Read-Host 'ID de la categoría Carrera'
```

```powershell
dotnet user-secrets set 'Boletos:CategoriaCarreraId' $categoriaCarreraId --project src/Promotora.WebApi
```

```powershell
dotnet user-secrets set 'Boletos:CategoriaCarreraId' $categoriaCarreraId --project src/Promotora.Worker
```

Genera una clave QR en memoria, compatible con Windows PowerShell 5.1. Conserva la misma clave mientras existan boletos válidos: cambiarla invalida su firma.

```powershell
$qrBytes = New-Object byte[] 32
```

```powershell
$qrRng = [Security.Cryptography.RandomNumberGenerator]::Create()
```

```powershell
$qrRng.GetBytes($qrBytes)
```

```powershell
$qrRng.Dispose()
```

```powershell
$qrKey = [Convert]::ToBase64String($qrBytes)
```

```powershell
dotnet user-secrets set 'Boletos:SigningKey' $qrKey --project src/Promotora.WebApi
```

```powershell
dotnet user-secrets set 'Boletos:SigningKey' $qrKey --project src/Promotora.Worker
```

```powershell
dotnet user-secrets set 'Outbox:Enabled' 'true' --project src/Promotora.Worker
```

```powershell
dotnet user-secrets set 'Compras:ConsumerEnabled' 'true' --project src/Promotora.Worker
```

```powershell
dotnet user-secrets set 'PagosSimulados:TasaFallo' '0' --project src/Promotora.Worker
```

Arranca la API y el Worker en terminales separadas. Las notificaciones se habilitan aparte con SMTP según [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md).

```powershell
dotnet run --project src/Promotora.WebApi
```

```powershell
dotnet run --project src/Promotora.Worker
```

## Swagger y comprobaciones

Inicia sesión como CLIENTE, completa cambio de contraseña si aplica y usa Authorize en /swagger según la guía frontend. Obtén evento y tipoEntradaId disponibles del catálogo. POST /api/v1/compras exige Idempotency-Key nuevo por compra lógica.

```json
{
  "eventoId": 1,
  "items": [{
    "tipoEntradaId": 1,
    "cantidad": 2,
    "asistentes": [
      { "nombreAsistente": "Persona Uno", "documentoIdentidad": "DOCUMENTO-PRUEBA-1" },
      { "nombreAsistente": "Persona Dos", "documentoIdentidad": "DOCUMENTO-PRUEBA-2" }
    ]
  }]
}
```

Los IDs del ejemplo se reemplazan por los de tu entorno. GET /api/v1/compras/{publicId} permite polling propio; GET /api/v1/boletos lista los emitidos. Repetir Idempotency-Key no reserva ni cobra de nuevo. Revisa Management: compras.procesar termina sin mensajes pendientes ni sin ACK; DLQ permanece vacía para compras válidas. Los asistentes se consultan desde PostgreSQL, no aparecen en el mensaje del broker.

Con 2 entradas de 100, se esperan subtotal 200, comisión 20, IVA 26.40 y total 246.40; dos boletos, un pago APROBADO y tres movimientos cuya suma es el total. Para probar rechazo, detén el Worker, configura TasaFallo=1, arráncalo y usa una nueva compra y clave: FALLIDA, cero boletos y cupo devuelto una vez. Restablece la tasa a 0 después. No provoca rechazos en compras gratuitas.

## Pruebas automáticas

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test -c Release
```

```powershell
dotnet format --no-restore --verify-no-changes
```

ProcesarCompraTests cubre RabbitMQ/PostgreSQL reales con el procesador financiero, duplicados, asistentes, rechazo y rollback/reintento. CompraAsistentesApiTests comprueba validación previa a reserva, agrupación y congelación. CompraConsumerTests conserva las pruebas aisladas de transporte/retry/DLQ. F6-08 ampliará caída/reinicio y fallos con efectos financieros; F6-09 hará carga de 500–1000 compras. No se cierran esas tareas con estas pruebas.