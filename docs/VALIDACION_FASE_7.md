# Validación de Fase 7 — Finanzas

## F7-07: conciliación tras escenarios mixtos

`ConciliacionFinancieraTests` compara todas las cuentas con SUM del ledger mediante una única consulta PostgreSQL. Así saldo y suma pertenecen al mismo snapshot; no se compara una página del extracto con el saldo completo.

El escenario secuencial ejecuta los adaptadores reales sobre PostgreSQL temporal:

1. Dos ventas de subtotal 300.00, comisión 30.00 e IVA 39.60 cada una: incremento conjunto de saldos 739.20.
2. Compra gratuita: sin movimientos ni cambio de saldos.
3. Reembolso total de una venta y repetición: incremento restante 369.60.
4. Retiro de 100.00 de la otra cuenta y repetición: incremento restante 269.60.
5. Retiro de los 200.00 restantes: incremento restante 69.60 (30.00 PLATAFORMA + 39.60 IMPUESTOS).
6. Cierre con saldo cero y repetición de un retiro confirmado: saldo sin cambios; un retiro nuevo se rechaza.
7. Consulta del estado CERRADA y extracto con VENTA +300.00, LIQUIDACION -100.00 y -200.00, suma cero.

Después de cada etapa comprueba `saldo == SUM(movimientos)` por cuenta, ausencia de sobregiro y el incremento global esperado. Usa el saldo inicial del fixture como base, evitando que el orden de los casos altere las expectativas.

El segundo escenario completa seis ventas y libera simultáneamente seis administradores distintos: tres reembolsan y tres liquidan todo el saldo del dueño. Cada uno repite su operación. El resultado conserva tres liquidaciones y nueve movimientos de reembolso; no duplica efectos. El incremento restante es 208.80: 90.00 en PLATAFORMA y 118.80 en IMPUESTOS; todas las cuentas siguen conciliadas.

No se siembran movimientos financieros artificiales para representar ventas, reembolsos o liquidaciones: se ejecutan finalizador, proveedor simulado durable y handlers financieros. Las bases son temporales y no utilizan credenciales del desarrollador.

Verificación F7-07: **416 pruebas Release aprobadas, 0 fallidas, 0 omitidas**, build sin warnings/errores y formato limpio. Dos casos nuevos de conciliación. F7-08 pendiente antes de cerrar la fase.

## Ejecutar localmente

Con Docker Desktop operativo, desde la raíz del repositorio:

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter FullyQualifiedName~ConciliacionFinancieraTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Prueba manual mediante [CONSULTAS_FINANCIERAS.md](CONSULTAS_FINANCIERAS.md), [REEMBOLSOS.md](REEMBOLSOS.md) y [LIQUIDACIONES.md](LIQUIDACIONES.md). Conservar las credenciales actuales y no editar saldos ni movimientos para forzar resultados.
