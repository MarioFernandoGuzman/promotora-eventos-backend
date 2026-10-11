# Evidencia de concurrencia — F5-09

Prueba: CompraConcurrenciaApiTests, HTTP real mediante WebApplicationFactory contra PostgreSQL temporal con Flyway y rol de aplicación restringido.

Ocho clientes autenticados distintos compiten desde una barrera asíncrona, con clave idempotente nueva por intención. Casos: 40 solicitudes sobre 7 lugares (una entrada por compra), 40 sobre 8 (dos por compra), 33 sobre 9 (tres por compra). Se esperan 7, 4 y 3 compras respectivamente y cupo final cero; el resto responde 409. No se usan esperas artificiales ni reloj del sistema para decidir cupo.

Además del HTTP, se comprueba que las cantidades de detalle sumen exactamente el cupo, exista una compra/auditoría por ganadora, los UUID sean únicos y no haya compras de solicitudes rechazadas. Montos D-15 congelados y componentes suman el total.

## Alcance actual y dependencia

La prueba original verifica reserva PENDIENTE. F6-09 amplía la evidencia con `CompraCargaApiTests`: 500 y 1000 solicitudes concurrentes, ocho clientes, cupo 100 y finalización real por outbox/RabbitMQ. Cada caso confirma exactamente 100 compras COMPLETADA, 100 pagos APROBADO, 100 boletos, 300 movimientos y Q12,320.00 conciliados; el resto responde 409 y el cupo termina en cero. No se sustituyen efectos financieros por dobles.

F6-11 repite ambos casos con el proveedor durable y V11. Suite: 360 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. F5-09 queda cerrada con estas ventas completas; tiempos históricos y comandos de carga en [PRUEBAS_F6_CARGA.md](PRUEBAS_F6_CARGA.md). Revisión de salida en [VALIDACION_FASES_5_6.md](VALIDACION_FASES_5_6.md).

## Comandos separados

Con Docker operativo, desde la raíz; no se usa la BD local de Compose:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter-class '*CompraConcurrenciaApiTests'
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

El cierre F5-09 no cambia credenciales ni añade endpoints. Contrato actual 202 + Location y uso manual en [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md); expiración en [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md). La migración V11 pertenece a F6-11, documentada en [RECUPERACION_COMPRAS.md](RECUPERACION_COMPRAS.md).
