# Dominio de compras y política monetaria — F5-01

D-15 fue fijada por el usuario y registrada antes del código. Rama `feature/f5-dominio-compras`, desde `origin/develop` en `c1966c0` (PR #14 integrado). Fase 5 sigue en curso: esta tarea aporta dominio y política, sin venta HTTP todavía. Swagger conserva las 53 operaciones existentes; `POST /compras` corresponde a F5-03.

## Módulos y responsabilidades

- Domain/Ticketing: `Compra`, `DetalleCompra`, `ImportesCompra` y `EstadoCompra`. Valida evento único, cantidad agregada contra máximo del evento, precios no negativos con 2 decimales, UUID/clave/moneda/fechas, importes compatibles con NUMERIC(10,2) y transiciones. Detalles/precios/componentes son inmutables y se copian desde las entradas recibidas.
- Domain/Finance: `PoliticaTarifaria`, record simple con tasas fraccionarias; no conoce IConfiguration, appsettings, EF ni servicios.
- Application: puerto `IPoliticaTarifariaProvider` y `CrearCompraFactory`, que entrega la política al agregado. Los futuros handlers obtendrán precios/evento desde puertos de catálogo; nunca confiarán en importes enviados por el cliente.
- Infrastructure: registro y proveedor de política a partir de tasas entregadas por el composition root. No añade tablas, columnas, migraciones ni paquetes.
- WebApi: lee tasas configuradas y registra la factory. `appsettings.json` contiene solo tasas públicas iniciales; los secretos siguen fuera del repositorio.

La factory de Application crea el agregado, pero no reserva cupo, guarda compras, cobra, audita ni emite boletos. F5-02 y las siguientes tareas incorporarán esos efectos y sus transacciones. `DetalleCompra` es una instantánea de una línea; los IDs de persistencia los asignará el repositorio, según Flyway.

## Dinero: D-15

Los precios base no incluyen IVA. Inicialmente `Finanzas:TasaIva = 0.12` y `Finanzas:ComisionPorcentaje = 0.10`; ambas configurables, sin carácter de definición o asesoría legal.

```text
subtotal  = suma(cantidad × precio_unitario)
comision  = Round(subtotal × ComisionPorcentaje, 2, AwayFromZero)
impuestos = Round((subtotal + comision) × TasaIva, 2, AwayFromZero)
total     = subtotal + comision + impuestos
```

Todo usa decimal. Se suma el subtotal de todas las líneas antes de aplicar tasas; se redondea por componente a nivel de compra. No se redondea cada precio para aceptar precisión inválida ni se recalcula el total multiplicando una tasa global.

| Subtotal | Comisión | IVA | Total |
|---|---|---|---|
| 300.00 | 30.00 | 39.60 | 369.60 |
| 33.33 | 3.33 | 4.40 | 41.06 |
| 0.00 | 0.00 | 0.00 | 0.00 |

Precios y componentes se congelan al crear la compra. `ImportesCompra.DesdeMontos` valida componentes persistidos sin aplicar tasas actuales. Cambiar configuración afecta compras nuevas. Reembolso total de F7 revertirá movimientos registrados; parcial fuera del alcance. Para total cero, `RequiereMovimientos` es false: el flujo financiero no deberá insertar movimientos cero (CHECK monto <> 0). El ledger aún se implementa en F7.

## Estados y fechas

```text
PENDIENTE → PROCESANDO → COMPLETADA
                 └──→ FALLIDA
    ├──→ CANCELADA
    └──→ EXPIRADA
```

Cancelar y expirar solo desde PENDIENTE; expiración desde `expira_en` inclusive. PROCESANDO registra `procesando_desde` en UTC y solo pasa a COMPLETADA/FALLIDA. Terminales no se reabren. Un estado esperado distinto devuelve conflicto y conserva estado/importes. Fechas llegan explícitamente desde Application (IClock en futuros handlers); Domain no obtiene la hora del sistema.

La validación del objeto no garantiza concurrencia en BD: el futuro adaptador debe ejecutar UPDATE WHERE estado = esperado, comprobar filas afectadas y devolver cupo únicamente si gana, en la misma transacción. Esto corresponde a F5-02/F5-06/F5-08; no se considera implementado todavía.

## Configuración y pruebas locales

El API lee `src/Promotora.WebApi/appsettings.json` con las tasas D-15. Variables de entorno/user-secrets pueden sustituirlas. Si el host no carga ese archivo, el composition root usa los mismos valores iniciales D-15. Se admiten tasas cero; negativas impiden registrar la política. El dominio rechaza cálculos que exceden el límite de las columnas.

Ejemplo de variables no sensibles, ejecutar por separado antes de levantar la API:

```powershell
$env:Finanzas__TasaIva = '0.12'
```

```powershell
$env:Finanzas__ComisionPorcentaje = '0.10'
```

```powershell
dotnet run --project src/Promotora.WebApi
```

Detener la API anterior con Ctrl+C antes de reiniciar. El fix de sentinels elimina los warnings de `DebeCambiarPassword` y `Publicado`; no requiere Flyway ni cambios de contraseña. No oculta el warning en producción: declara HasSentinel(false). La prueba convierte ese warning en error y confirma true/false en PostgreSQL real.

Para comprobar el dominio y toda la solución, desde la raíz con Docker encendido:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --project tests/Promotora.Domain.Tests --no-build -c Release --filter-class '*CompraTests'
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Se cubren D-15 (incluido caso gratuito), midpoint/redondeo a nivel de compra, 2.000 subtotales aleatorios reproducibles, overflow/precisión/tasas inválidas, evento ajeno, límite agregado, copia inmutable, cambio de política solo para compras nuevas, expiración/cancelación y estados finales. Pruebas de DI confirman tasas alternativas y rechazo de negativas. Evidencia TRX en TestResults (ignorado por Git).

Resultado local final: build Release con 0 warnings/errores, **137 pruebas aprobadas**, 0 fallidas/omitidas y format limpio. Domain 33, Application 5, Infrastructure 33, API 54 y Architecture 12. Los tests de defaults booleanos convierten el warning en error, sin silenciarlo en producción. CI remoto queda por confirmar cuando el usuario publique el PR.

Revisión del alcance contra AGENTS.md §§5/12: sin cambios de esquema, secretos, dependencias nuevas, borrados ni publicación RabbitMQ. Domain puro y controllers sin lógica añadida. Reserva, idempotencia persistida, ownership HTTP, pago, QR, ledger y reembolso conservan sus tareas futuras. Fase 5 no está cerrada.
