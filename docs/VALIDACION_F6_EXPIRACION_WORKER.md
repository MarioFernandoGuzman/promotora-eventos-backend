# Revisión F6-07

Rama feature/f6-expiracion-worker desde origin/develop 317fbc9, después del merge del PR #21. Job periódico en Worker reutiliza el handler de F5-08; coordinación en la misma transacción PostgreSQL mediante pg_try_advisory_xact_lock con clave fija PROM/607.

## Evidencia

- dotnet build --no-restore -c Release -warnaserror: cero warnings/errores.
- dotnet test --no-build -c Release --report-trx: 267 pruebas, cero fallos/omisiones (Domain 39, Application 5, Infrastructure 113, API 98, Architecture 12).
- dotnet format --no-restore --verify-no-changes limpio.
- Once pruebas nuevas de lock no bloqueante, liberación por commit/rollback/dispose, dos hosts reales, omisión con lock ocupado, lotes, exclusión de vigentes/PROCESANDO, rollback en shutdown, reintento con scope nuevo, opciones inválidas y cancelación/transacción obligatoria.
- Regresión F5-08: carreras de cancelar/procesar entre consulta y UPDATE, devolución única y rollback de lote.

## AGENTS.md §§5/12

Solo PENDIENTE vencidas son candidatas; el UPDATE revalida estado y vencimiento. Solo el ganador devuelve cupo y audita, en la misma transacción. El advisory lock coordina expirar contra expirar; no sustituye la transición condicional contra cancelar/procesar. Bloqueo liberado con el fin de la transacción, sin locks de sesión retenidos en el pool.

Puertos en Application, SQL en Infrastructure, host/DI en Worker; no cambios en controllers ni contratos HTTP. IClock UTC, cancelación async, logs genéricos y scopes nuevos por ciclo. Sin nuevos paquetes, tablas/columnas, migraciones editadas, secretos ni DELETE sobre datos protegidos. No hay cobros, reembolsos, ledger ni publicación RabbitMQ en este job.

## Límites y pendientes

Expiracion:Enabled=false por defecto; habilitar exige conexión PostgreSQL del Worker. Un lote por ciclo, intervalo después del trabajo: backlog y fallos pueden retrasar expiración respecto a expiraEn. PROCESANDO sigue fuera; su recuperación es F6-11. La finalización financiera F7, F6-03/F6-04, F6-08/F6-09/F6-10/F6-11 y la salida completa de fase permanecen pendientes.

Sin incumplimientos detectados dentro del alcance F6-07. Configuración, módulos, pruebas y verificación por Swagger sin alterar estados manualmente: [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md).
