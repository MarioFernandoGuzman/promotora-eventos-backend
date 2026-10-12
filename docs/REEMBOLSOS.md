# Reembolsos totales — F7-04

## Contrato aprobado

`POST /api/v1/compras/{publicId}/reembolsar`, sin body, solo ADMINISTRADOR activo con contraseña inicial cambiada. No lo ejecutan CLIENTE ni DUENO_RECINTO. El handler revalida al usuario actual desde PostgreSQL, además de las policies JWT.

Respuesta 200, también al repetir sin efectos nuevos:

```json
{
  "publicId": "11111111-1111-1111-1111-111111111111",
  "estadoPago": "REEMBOLSADO",
  "montoReembolsado": 369.60,
  "moneda": "GTQ"
}
```

| HTTP | Caso |
|---|---|
| 200 | Reembolso completo o repetición con respaldo consistente |
| 400 | publicId vacío |
| 401/403 | Sin autenticación, rol incorrecto, usuario inhabilitado o cambio de contraseña pendiente |
| 404 | Compra inexistente |
| 409 | Compra no COMPLETADA, respaldo financiero/emisión inconsistente, cuenta afectada cerrada o saldo insuficiente |

La compra conserva COMPLETADA: no existe un estado REEMBOLSADA en el esquema. El pago pasa APROBADO → REEMBOLSADO. El frontend usa `estadoPago` de esta respuesta para confirmar el resultado financiero; GET de compras mantiene su contrato y estado. Reembolso parcial fuera de alcance. No se necesita Idempotency-Key adicional: el pago y la compra bloqueada identifican el único reembolso total.

## Módulos y transacción

Domain/Finance incorpora `MovimientosReembolso`, que valida los componentes originales contra los importes congelados y los invierte exactamente. No recalcula IVA/comisión con configuración vigente. Cada componente distinto de cero genera un REEMBOLSO negativo en su cuenta original; componentes cero no generan movimientos.

Application/Finance incorpora `ReembolsarCompraCommand/Handler`, validator, DTO y puerto `IReembolsoRepository`. Infrastructure implementa consulta, locks y UPDATE condicionales. WebApi añade `ReembolsosController` con la ruta aprobada; no contiene EF, SQL ni lógica financiera. Sin paquetes, columnas ni migraciones nuevas.

Orden: bloquear usuario y compra, comprobar pago/emisión/ledger, bloquear cuentas por ID, verificar conciliación y saldo. La transición del pago, movimientos contrarios, saldos, anulación de boletos, devolución de cupo y auditoría comparten una transacción. No se permite saldo negativo ni movimientos en cuentas cerradas. Repetir un reembolso confirmado verifica su respaldo y devuelve 200 incluso si una cuenta se cerró después: no intenta escribir otro movimiento.

La anulación es `UPDATE ... WHERE estado='VALIDO' RETURNING id,tipo_entrada_id`. Solo esas filas devuelven cupo, mediante incremento atómico por tipo de entrada. USADO conserva estado, fecha_uso y validado_por. ANULADO previo no devuelve cupo de nuevo. Un escaneo usa lock compartido de compra y el reembolso lock exclusivo: si gana el escaneo, ese boleto queda USADO; si gana el reembolso, el QR se rechaza. El importe se devuelve totalmente en ambos casos, según la política aprobada.

Auditoría: CREATE de los movimientos contrarios, UPDATE de los boletos realmente anulados y REFUND de pago/compra. No registra QR, tokens, contraseñas ni cuerpos. Repetir no añade auditorías. El ledger original nunca se edita ni elimina.

**Alcance simulado:** no hay transferencia bancaria ni llamada a una pasarela externa de devolución. `pagos_simulados` conserva el recibo original inmutable; el estado operativo del pago y el ledger registran el reembolso del proyecto. No se añade correo, cola ni evento de reembolso. El crédito de cuentas cerradas o sin saldo necesita un proceso autorizado aparte; este endpoint devuelve 409 y deja intacto el negocio.

## Probar en Swagger local

Primero sigue [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md) para completar una compra por Worker y [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md) para autenticar Swagger. Conserva tus credenciales actuales; no se crean usuarios ni contraseñas nuevos.

Desde la raíz del repositorio, en terminales con los secretos actuales:

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
dotnet run --project src/Promotora.WebApi
```

En otra terminal con conexión, firma QR, categoría, RabbitMQ y consumidores configurados según la guía asíncrona:

```powershell
dotnet run --project src/Promotora.Worker
```

1. En Swagger, login como CLIENTE y Authorize con su JWT. Crear compra con una clave nueva y consultar Location hasta COMPLETADA. Anotar publicId y, si quieres probar entrada, un QR de mis boletos (no pegarlo en logs o documentos).
2. Login como ADMINISTRADOR habilitado y sustituir el JWT en Authorize. Si es el bootstrap inicial, completar el cambio obligatorio de contraseña según la guía de autenticación.
3. Opcionalmente validar un boleto antes del reembolso para verificar que USADO conserve su historial y no libere cupo.
4. Ejecutar POST /api/v1/compras/{publicId}/reembolsar sin body. Esperar estadoPago REEMBOLSADO e importe original total. Repetir: misma respuesta sin otro reembolso.
5. Como CLIENTE propietario, consultar sus boletos: los restantes VALIDO ahora son ANULADO. GET de compra continúa mostrando COMPLETADA. El QR anulado devuelve 409 al intentar validarlo; uno ya USADO también sigue rechazando entradas posteriores.
6. Como CLIENTE o DUENO_RECINTO, intentar reembolsar debe dar 403. No hacer cambios SQL manuales para alterar saldos o forzar estados en tu base local; los escenarios de fallo tienen pruebas aisladas.

Si se obtiene 409, revisar estado, pago único, ledger/saldos, cuentas activas y emisión completa. Nunca modificar movimientos originales para eludir esa validación.

## Pruebas automáticas

Con Docker Desktop operativo, sin iniciar API/Worker ni usar credenciales locales:

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Domain.Tests --no-build -c Release --filter FullyQualifiedName~MovimientosReembolsoTests --report-trx
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter FullyQualifiedName~ReembolsoTests --report-trx
```

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter FullyQualifiedName~ReembolsosApiTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Las ventas de prueba se completan con los adaptadores financieros reales y el proveedor durable, no con efectos financieros artificiales. Se cubren reembolso con boletos válidos/usados/anulados, gratuito/repetido, veinte administradores distintos, compras distintas con cuentas compartidas, rollback en cuatro etapas de auditoría, saldo insuficiente/cierre, contrato/permisos/Swagger y tres carreras HTTP con escaneo. Las pruebas de insuficiencia preparan una liquidación como dato del escenario: no implementan ni marcan F7-05.

La suite completa limita las fixtures de API e Infrastructure a cuatro por ensamblado con el [algoritmo conservador de xUnit](https://xunit.net/docs/running-tests-in-parallel). Evita agotar los pools de redes de Docker al arrancar muchas bases simultáneamente. Las 20 solicitudes de reembolso y las carreras internas siguen concurrentes; no se serializan ni se omiten. No requiere borrar redes o volúmenes del desarrollador.

Verificación del bloque: **387 pruebas Release aprobadas, 0 fallidas, 0 omitidas**, build con cero warnings/errores y formato limpio. 27 casos nuevos: ocho de dominio, trece de infraestructura y seis de API. Swagger tiene 61 operaciones; la nueva ruta documenta ejemplo, autorización y códigos de respuesta. El ajuste de fixtures permitió ejecutar la suite completa sin modificar el Docker Compose local.
