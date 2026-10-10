# Evidencia de idempotencia — F5-10

CompraIdempotenciaApiTests ejerce el contrato HTTP actual sobre PostgreSQL real temporal con Flyway/rol restringido. Las claves, usuarios y contraseñas de prueba se generan en memoria; no modifica Compose.

## Casos

- Cuarenta solicitudes propias simultáneas con la misma clave: un 201 y treinta y nueve 200, mismo publicId/montos/detalles; una compra, un detalle, una auditoría CREATE y un único descuento de cupo.
- Dos clientes disputan una clave: uno crea, sus repeticiones recuperan la misma compra, el otro recibe 409 sin información de la compra y 404 al consultarla. Una sola compra/reserva.
- Reutilizar una clave después de cancelar, cambiando precio y cantidad de la solicitud: devuelve la compra CANCELADA original, con montos congelados; no vuelve a reservar ni auditar CREATE.
- Un 409 por cupo no consume la clave ni deja compra parcial. Tras liberar cupo cancelando otra compra, reintentar crea una compra y repetir la misma clave la recupera.

F5-10 comprueba una compra por Idempotency-Key en el flujo persistido actual. La idempotencia de consumidores corresponde a F6 y la de finalización financiera/boletos debe ampliarse con F7-02/F7-03. No se declara completado el criterio de salida de Fase 5; F5-09 permanece pendiente de su comprobación de ventas completadas.

## Comandos separados

Con Docker operativo, después de build Release:

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter-class '*CompraIdempotenciaApiTests'
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Prueba manual en Swagger: iniciar sesión CLIENTE, crear con una clave nueva y repetirla. Esperar el mismo publicId sin otro descuento. Cancelar y repetir la clave devuelve CANCELADA; para una nueva intención generar otra clave. No cambia credenciales ni añade configuración. Contratos y pasos locales: [COMPRAS_API.md](COMPRAS_API.md).
