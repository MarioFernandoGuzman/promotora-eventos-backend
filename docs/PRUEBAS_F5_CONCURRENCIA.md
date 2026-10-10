# Evidencia de concurrencia — F5-09

Prueba: CompraConcurrenciaApiTests, HTTP real mediante WebApplicationFactory contra PostgreSQL temporal con Flyway y rol de aplicación restringido.

Ocho clientes autenticados distintos compiten desde una barrera asíncrona, con clave idempotente nueva por intención. Casos: 40 solicitudes sobre 7 lugares (una entrada por compra), 40 sobre 8 (dos por compra), 33 sobre 9 (tres por compra). Se esperan 7, 4 y 3 compras respectivamente y cupo final cero; el resto responde 409. No se usan esperas artificiales ni reloj del sistema para decidir cupo.

Además del HTTP, se comprueba que las cantidades de detalle sumen exactamente el cupo, exista una compra/auditoría por ganadora, los UUID sean únicos y no haya compras de solicitudes rechazadas. Montos D-15 congelados y componentes suman el total.

## Alcance actual y dependencia

F5-09 valida la concurrencia de la reserva disponible en POST /compras. Las filas ganadoras son PENDIENTE: no se llaman ventas cobradas ni se simula que el flujo financiero está terminado. No se generan pagos/ledger en producción. La prueba de compras COMPLETADA, pagos, movimientos y boletos se debe ampliar al conectar F7-02/F7-03, antes de declarar el criterio de salida de Fase 5. Esta dependencia mantiene el orden ya acordado para F5-04/F5-05.

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

No cambia usuario/contraseña ni añade endpoint o migración. Swagger sigue con 59 operaciones; uso manual de compras en [COMPRAS_API.md](COMPRAS_API.md), expiración en [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md).
