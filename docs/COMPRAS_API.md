# Compras: reserva e idempotencia — F5-01 a F5-03

Este bloque añade una operación en Swagger: `POST /api/v1/compras`. Crea una compra PENDIENTE, reserva cupo y congela precios/componentes D-15. Todavía no cobra ni emite boletos: F5-04/F5-05 incorporarán esos pasos. GET de compra, cancelación y job de expiración corresponden a F5-06/F5-08. El Worker aún no procesa estas compras; el contrato asíncrono 202 + outbox llega en F6-03. No usar esta etapa para ventas reales.

## Módulos y transacción

| Capa | Responsabilidad implementada |
|---|---|
| Domain/Ticketing y Finance | Compra, detalles, estados y cálculo decimal D-15 |
| Application/Ticketing | Handler, FluentValidation, factory y puertos; usuario real/propiedad, ventana y máximo |
| Infrastructure/Ticketing | Reserva atómica, devolución condicional y persistencia; transacción con reintentos |
| WebApi | Controller, JWT/policies, rate limiting, Swagger con ejemplo y errores |

El cliente no elige clienteId, estado, moneda, precio, subtotal, comisión, impuestos ni total. El handler usa el usuario de la sesión, revalida su estado/rol/password bajo bloqueo, obtiene su perfil CLIENTE y consulta los precios reales del catálogo. Agrupa tipos repetidos antes de aplicar el máximo agregado y reservar.

Orden de bloqueo: usuario → recinto → evento → categoría → localidades ordenadas → tipos ordenados. SHARE sobre los padres permite compras simultáneas mientras impide cambios de configuración; las entradas se bloquean para congelar su precio. El descuento sigue siendo `UPDATE` condicional y verifica filas afectadas. No se decide el cupo leyendo y comparando en C#.

Reserva, compra, detalles y auditoría CREATE se confirman juntos. Si una línea falla, se revierte todo. Se reintenta la transacción completa hasta tres intentos ante 40001/40P01 y ante carrera del UNIQUE de idempotencia; cada intento vuelve a comprobar actor/propiedad. No se publican mensajes, ni se insertan pagos, boletos o ledger en esta etapa. No se añadieron migraciones ni paquetes.

## Contrato para frontend

Requiere CLIENTE activo y sesión completa. Administrador/dueño no pueden comprar con sus cuentas. El administrador puede preparar el catálogo; para comprar debe iniciar sesión con otra cuenta CLIENTE.

Header obligatorio: `Idempotency-Key`, texto no vacío de hasta 100 caracteres. Generar una clave por intención de compra y conservarla para reintentos de esa misma intención. Una compra nueva requiere otra clave. La comparación es exacta, sin trim ni cambio de mayúsculas.

```http
POST /api/v1/compras
Authorization: Bearer <accessToken del cliente>
Idempotency-Key: <UUID generado para esta intención>
Content-Type: application/json
```

```json
{"eventoId":1,"items":[{"tipoEntradaId":1,"cantidad":3}]}
```

Sustituir IDs por los de un evento EN_VENTA y tipos activos de ese mismo evento. Recinto, categoría y localidad asociada deben estar activos. Ventana UTC `[fechaInicioVenta, fechaFinVenta)`, verificada nuevamente después de esperar los bloqueos. Ambas fechas deben existir. Cantidades positivas; el total de todas las líneas no puede superar maxBoletosPorCompra.

Respuesta nueva: HTTP 201. Con entrada a Q100 y cantidad 3:

```json
{
  "publicId":"<UUID de la compra>",
  "eventoId":1,
  "estado":"PENDIENTE",
  "fechaCompra":"<fecha UTC>",
  "expiraEn":"<fecha UTC de expiración>",
  "moneda":"GTQ",
  "subtotal":300.00,
  "comisionPlataforma":30.00,
  "impuestos":39.60,
  "total":369.60,
  "items":[{"tipoEntradaId":1,"cantidad":3,"precioUnitario":100.00,"subtotal":300.00}]
}
```

Repetir una clave propia devuelve HTTP 200 y la compra persistida, incluso si el precio, la ventana, el estado del evento o el cuerpo válido cambiaron. No recalcula importes, no reserva de nuevo y no duplica auditoría. Si la clave pertenece a otro cliente, devuelve 409 sin datos de esa compra. Un cliente desactivado no puede recuperar compras con su JWT anterior (401); el handler también revalida al actor dentro de la transacción. El clienteId interno no aparece en la respuesta.

| Código | Causa |
|---|---|
| 400 | Header ausente/vacío/largo, JSON inválido, items vacíos/nulos, IDs o cantidades no positivos |
| 401 | Sin sesión, JWT vencido/inválido o usuario desactivado |
| 403 | Cuenta que no es CLIENTE o sesión que exige cambio de contraseña |
| 404 | Evento inexistente |
| 409 | Cupo insuficiente, ventana/estado no habilitado, padre del evento inactivo, clave ajena o conflicto concurrente |
| 422 | Límite agregado, tipo de otro evento/inactivo, localidad inactiva o regla monetaria/integridad |
| 429 | Límite de solicitudes; respetar Retry-After |

Errores usan application/problem+json y traceId. El frontend debe mostrar cada componente y enviar únicamente IDs/cantidades. La disponibilidad pública es orientativa: solo POST confirma reserva.

## Probar localmente, en orden

1. Preparar conexión, JWT y bootstrap siguiendo [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md). Las credenciales existentes siguen vigentes; este bloque no cambia contraseñas. Registrar/iniciar sesión con CLIENTE según [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md).
2. Con Docker operativo, ejecutar cada comando por separado desde la raíz:

```powershell
docker compose up -d --wait
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
dotnet build -warnaserror
```

```powershell
dotnet test --report-trx
```

```powershell
dotnet format --verify-no-changes
```

```powershell
dotnet run --project src/Promotora.WebApi
```

3. Abrir http://localhost:5080/swagger. Como administrador/dueño, preparar recinto, evento, tipos a Q100 y cupo suficiente, programar y abrir venta; seguir [CATALOGO_EVENTOS.md](CATALOGO_EVENTOS.md). Elegir fechas que incluyan el momento de la prueba, en UTC. Como cliente, consultar GET `/api/v1/catalogo/eventos/{id}` para copiar eventoId y tipoEntradaId.
4. Login como CLIENTE, copiar accessToken y pegarlo sin prefijo Bearer en **Authorize**. Abrir Compras → POST → Try it out. Indicar una clave nueva y el JSON con cantidad 3. Esperar 201, PENDIENTE y total Q369.60.
5. Ejecutar exactamente la misma clave otra vez: 200 y mismo publicId. Consultar nuevamente el catálogo: el cupo se descontó una sola vez. Con nueva clave y cantidad mayor al disponible, esperar 409; con máximo excedido, 422. Sin clave, 400; como administrador, 403.
6. Las compras quedan reservadas en esta etapa. expiraEn se guarda, pero todavía no se devuelve cupo automáticamente: requiere F5-08. Para repetir pruebas sin agotar el tipo, preparar un nuevo evento de desarrollo; no borrar compras ni modificar manualmente su estado/cupo.

Para generar solo una clave de compra en PowerShell:

```powershell
[guid]::NewGuid().ToString()
```

Configuraciones públicas opcionales (antes de iniciar API, reiniciar con Ctrl+C si ya corre):

```powershell
$env:Ticketing__ReservaMinutos = '15'
```

```powershell
$env:RateLimiting__CompraPermitLimit = '20'
```

Duración inicial de reserva: 15 minutos, configurable y positiva. Moneda actual GTQ, como el esquema/catálogo. Rate limit inicial: 20 solicitudes por minuto por IP, sin cola, independiente de login/registro; acepta configuración entre 1 y 1000. Las tasas D-15 siguen Finanzas:TasaIva=0.12 y Finanzas:ComisionPorcentaje=0.10. Ninguna configuración requiere guardar secretos en documentos.

## Pruebas y alcance restante

Validación local del bloque: 159 pruebas Release en verde (33 Domain, 5 Application, 38 Infrastructure, 71 API y 12 Architecture), build con cero warnings y format limpio. Las pruebas API ejecutan handlers, transacciones y auditoría contra PostgreSQL temporal con Flyway. Cubren componentes D-15/congelación, agrupación, idempotencia propia concurrente, carrera de clave entre clientes, rollback de reserva parcial y por fallo de auditoría, ventana, límites, entradas/recursos inactivos, sesión/roles (también en el handler sin controller), compra gratuita, rate limiting y contrato Swagger. F5-02 prueba directamente 20 reservas sobre 5 lugares y devolución única entre 20 cancelaciones/expiraciones.

F5-09/F5-10 siguen pendientes: aportarán la validación final del flujo completo de pago/emisión bajo concurrencia e idempotencia, además de estas regresiones iniciales. Fase 5 aún no cumple su criterio de salida. Facturación electrónica y reembolsos parciales permanecen fuera de alcance.
