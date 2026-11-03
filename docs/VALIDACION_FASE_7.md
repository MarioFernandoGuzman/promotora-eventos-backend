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

Verificación histórica F7-07: **416 pruebas Release aprobadas, 0 fallidas, 0 omitidas**, build sin warnings/errores y formato limpio. Dos casos nuevos de conciliación.

## F7-08: permisos y trigger del ledger

`InmutabilidadLedgerTests` prepara una venta real en PostgreSQL temporal y apunta UPDATE/DELETE a su movimiento VENTA existente. Comprueba por separado las dos barreras:

- Con test_app, miembro de promotora_app: SELECT/INSERT permitidos, UPDATE/DELETE sin privilegios; rechazo SQLSTATE 42501.
- Con SET LOCAL ROLE flyway, propietario de la tabla: UPDATE/DELETE tienen privilegios, pero el trigger protege la fila; rechazo SQLSTATE 23514 con el mensaje de solo INSERT.

Cada intento tiene una transacción explícita que se revierte. Después verifica que ID, cuenta, compra, tipo, monto, fecha y descripción originales siguen iguales, que la venta conserva sus tres movimientos y que saldo/SUM coinciden. No utiliza WHERE false como única evidencia: el objetivo es una fila existente.

La conexión privilegiada procede exclusivamente del contenedor de la fixture y mantiene su credencial aleatoria en memoria. No utiliza PostgreSQL local ni modifica roles, permisos, triggers o migraciones. Estos intentos negativos se ejecutan únicamente en las pruebas autorizadas; no repetir UPDATE/DELETE manualmente en la base del desarrollador.

## Cierre y revisión del diff

Verificación final: **420 pruebas Release aprobadas, 0 fallidas, 0 omitidas**; build sin warnings/errores y formato limpio. F7-06–F7-08 añaden once casos: cinco API, dos de conciliación y cuatro de inmutabilidad. Swagger tiene 65 operaciones. Las ocho tareas de Fase 7 quedan completas localmente.

Se revisó el diff completo de feature/f7-consultas-conciliacion frente al criterio «el dinero cuadra siempre y cada centavo es trazable» y AGENTS.md §§5/12:

- Las consultas solo leen, verifican usuario y ámbito de dueño en Application, usan UTC/CT y no exponen referencias internas del ledger. Controllers sin EF/SQL; puertos de Application implementados en Infrastructure.
- Los escenarios financieros reales mantienen saldo/SUM por cuenta, componentes congelados, reintentos sin doble efecto y trazabilidad por movimiento/pago/compra y auditorías existentes.
- Ledger protegido por permisos y trigger; las únicas mutaciones intentadas en este bloque son pruebas negativas autorizadas, aisladas y revertidas.
- No se editaron migraciones ni esquemas public/bi, no se agregaron paquetes, secretos o binarios y no se habilitaron endpoints fuera del contrato aprobado.
- Casillas y referencias del plan actualizadas; guías de cuentas/liquidaciones enlazan las consultas disponibles. Los conteos anteriores del plan se identifican como evidencia histórica.

**Incumplimientos detectados en el diff: ninguno.** Límites del alcance: cobro/reembolso/liquidación simulados sin transferencia externa; idempotencia de liquidaciones depende del protocolo de bloqueo y descripción reservada, sin índice UNIQUE adicional; consultas operativas paginadas sin snapshot entre peticiones. La conciliación es evidencia de pruebas y consulta manual, no un job administrativo automático. CI remoto queda por verificar al publicar el PR.

## Ejecutar localmente

Con Docker Desktop operativo, desde la raíz del repositorio:

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter FullyQualifiedName~ConciliacionFinancieraTests --report-trx
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter FullyQualifiedName~InmutabilidadLedgerTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Prueba manual mediante [CONSULTAS_FINANCIERAS.md](CONSULTAS_FINANCIERAS.md), [REEMBOLSOS.md](REEMBOLSOS.md) y [LIQUIDACIONES.md](LIQUIDACIONES.md). Conservar las credenciales actuales y no editar saldos ni movimientos para forzar resultados.
