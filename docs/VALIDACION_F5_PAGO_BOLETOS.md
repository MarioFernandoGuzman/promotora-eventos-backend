# Validación de F5-04 y F5-05

Base: `b1df7b9` de develop. Rama: `feature/f5-pago-boletos`.

## Alcance y resultados

F5-04 incorpora el puerto de pago simulado, tasa de fallo configurable, referencia idempotente `compras.public_id` y consulta del resultado. F5-05 incorpora el servicio interno de emisión, firma HMAC-SHA256 y datos de asistentes. No se añadieron endpoints, paquetes ni migraciones.

- `dotnet build --no-restore -warnaserror -c Release`: sin advertencias ni errores.
- `dotnet test --no-build -c Release --report-trx`: 187 pruebas aprobadas, sin fallos ni omisiones: Domain 39, Application 5, Infrastructure 60, API 71, Architecture 12.
- `dotnet format --verify-no-changes --no-restore`: limpio.

Las pruebas cubren repetición y concurrencia del pago, configuración inválida, cancelación, rechazo simulado, alteración del QR, clave ausente, veinte emisiones concurrentes, asistentes obligatorios en carreras, repetición sin duplicados, rollback ante fallo de auditoría y rechazo sin ledger o con saldo inconsistente. Renombrar la categoría no elimina el requisito de asistentes: se identifica por ID estable.

## Revisión contra AGENTS.md §§5 y 12

- Emisión serializada mediante bloqueo de la compra dentro de la transacción. Repetir una emisión completa devuelve los boletos existentes, conservando estados y datos; una emisión parcial exige conciliación.
- No se modifica cupo ni estado de compra. No hay borrados ni escritura de ledger en este bloque.
- Para emitir por primera vez se exige COMPLETADA, pago aprobado para importes positivos, componentes congelados correctos, cuentas correspondientes y saldos iguales a la suma de sus movimientos. Total cero no exige movimientos.
- Boletos y auditoría se insertan en la misma transacción; no se registran tokens QR completos ni datos personales en la auditoría.
- Fechas UTC mediante IClock, operaciones asíncronas con CancellationToken y puertos definidos en Application; controllers sin acceso a persistencia.
- Clave QR externa independiente de JWT, sin secretos versionados. Las migraciones existentes y el esquema bi no cambian.

No se detectaron incumplimientos en el alcance de estos componentes. El criterio de salida de Fase 5 aún está pendiente: faltan tareas posteriores y la integración financiera descrita a continuación.

## Dependencias pendientes

El usuario delegó resolver la dependencia entre emisión y finanzas. Se mantiene el orden del plan: se preparan y prueban los componentes, y la finalización se conecta cuando F7-02/F7-03 permitan registrar pago, ledger, estado COMPLETADA y boletos en una sola transacción. El método EnTransaccionAsync permite esa integración sin abrir otra transacción. POST /compras sigue creando PENDIENTE; no cobra ni emite boletos todavía.

El simulador guarda resultados en memoria del proceso. Reiniciar pierde su historial y API/Worker no comparten ese historial. Antes de habilitar recuperación F6-11 se necesita consulta durable; un resultado desconocido no demuestra ausencia de pago.

Configurar Boletos:SigningKey y Boletos:CategoriaCarreraId antes de usar emisión. Sin configuración válida se rechaza la operación. La identificación por categoría usa el esquema existente; no añade columnas.

## Guías locales

- [Pago simulado](PAGOS_SIMULADOS.md).
- [Emisión, clave QR, asistentes y comandos de prueba](BOLETOS_EMISION.md).
- [Contrato actual de compras para frontend](COMPRAS_API.md).
