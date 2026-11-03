# Validación de reportes y preparación de Power BI

Fecha: 03/11/2026. Rama: `feature/f10-reportes-bi`, creada desde `origin/develop` integrado e224e5b. Commits locales por tarea, sin push/merge. El cambio local previo de `.gitignore` permanece fuera de los commits.

## Estado real

F10-01–05 y F10-07 completados: 15 reportes en `/api/v1/reportes/{codigo}`, JSON paginado, exportaciones Excel/PDF auditadas. F10-06 implementado como proyecto revisable y conexión restringida, **pendiente de aceptación visual/refresh y copia PBIX por el usuario**, quien solicitó los pasos para comprobarlo personalmente. Q10 satisfecho; Q11 y la salida completa de Fase 10 no se cierran todavía. F11 no se inicia.

## Evidencia del backend

- `dotnet build -c Release --no-restore -warnaserror -m:1`: aprobado, cero warnings/errores.
- `dotnet format --verify-no-changes --no-restore`: aprobado. Después solo cambiaron dos literales de expectativas de pruebas; no se cambió formato de código.
- Una ejecución de `dotnet test -c Release --no-build --report-trx`: 534 casos, 532 aprobados y dos fallos de expectativas de prueba; 0 omitidos, 4m47s. Dominio, Application, arquitectura e infraestructura aprobaron. Los fallos fueron una acción de auditoría de prueba no admitida y el contador Swagger anterior (86).
- Se corrigieron únicamente esas expectativas: acción CREATE válida y 87 operaciones REST. Se reejecutó cada método afectado: ambos aprobados (1/1, 1/1), sin repetir toda la suite ni modificar reglas de producción para acomodar pruebas. Resultado combinado: los 534 casos comprobados. No se presenta como una segunda ejecución completa en verde.
- TRX locales ignorados: `TestResults/Promotora.*.trx`, `reportes-exportacion-corregida.trx` y `reportes-swagger-corregido.trx`. CI remoto pendiente al publicar el PR.

Las cinco pruebas API nuevas agrupan escenarios necesarios para evitar ejecuciones redundantes:

| Caso | Resultado conocido |
|---|---|
| Ventas/ocupación | 300 subtotal + 30 comisión + 39.60 IVA = 369.60 GTQ; ocupación 3/5; total conservado en página vacía |
| Asistencia/reembolso | Tres emitidos; tras reembolso tres anulados, ingresos 300→0 y comisiones 30→0; bruto 369.60 permanece |
| Cancelación | Una compra CANCELADA, importe intentado 246.40 y cero pagos/ventas cobradas |
| Operación | Calificación aprobada 5, un evento, tres inscritos 5K y uno validado; IA sin conversaciones y total de tiempo no inventado |
| Exportación | XLSX leído con ClosedXML y PDF con PdfPig; 369.60 en ambos, dos EXPORT persistidos, texto `=SUM(1,1)` no tratado como fórmula, 501 grupos PDF rechazan con 422 sin nueva auditoría de éxito |

Se verificaron además 401/403, filtros incompatibles, paginación acotada, hasta exclusivo UTC y Swagger con JSON/PDF/XLSX. No hay llamadas cloud, reindexación, pasarelas externas ni pruebas de carga nuevas. PostgreSQL y compras simuladas de pruebas se ejecutan en contenedores temporales.

## Evidencia de BI

Power BI Desktop se instaló gratuitamente desde Microsoft Store, autorizado por el usuario. El LOGIN local `promotora_bi`, miembro de `bi_reader`, se creó por solicitud expresa con contraseña aleatoria privada en `.env.powerbi` ignorado. No se altera ningún usuario de la aplicación ni se guardan credenciales en el modelo.

`Initialize-PowerBi.ps1 -SoloVerificar` aprobado también bajo Windows PowerShell 5: permisos efectivos, autenticación TCP, SELECT de vista permitido y SELECT de usuarios rechazado con SQLSTATE 42501. La verificación no imprime la contraseña y no considera cualquier error de conexión como prueba de denegación.

Proyecto `bi/Promotora.pbip`: 16 vistas, 16 páginas (Resumen + R-01…R-15), tablas, gráficos, filtros y medidas DAX. Referencias de columnas explícitas, promedios ponderados y medidas monetarias vacías al combinar monedas. Validación oficial Microsoft `powerbi-report-authoring-cli` 0.5.0: resultado succeeded, cero errores/advertencias. Esa validación de archivos no sustituye comprobar render/DAX/actualización en Desktop. Las primeras aperturas dieron errores de definición; se ajustaron estructura/tema/versiones/identificadores y el usuario comprobará la versión final. No hay PBIX final ni refresh acreditado.

## Revisión de diff contra AGENTS.md §§5 y 12

- Clean Architecture conservada: controller llama handlers; Application define puertos y validadores; SQL, EF/ADO.NET y bibliotecas de exportación solo en Infrastructure. La regla de dependencia pasó su suite.
- SQL solo sobre las vistas `bi` existentes, parámetros para valores e identificadores definidos en catálogo interno. No SQL recibido del cliente. Snapshot Repeatable Read para filas/conteos/totales.
- No cambios en migraciones, esquema operativo, vistas, reservas, QR, outbox, consumidores, estados de negocio ni ledger. No se agregaron tablas/columnas/rutas fuera del alcance aprobado. El LOGIN BI se provisiona fuera de migraciones como exige la política existente.
- Acceso solo ADMINISTRADOR habilitado en controller y handler; R-09 no se expone a dueños/clientes. Auditoría EXPORT persistida antes de entregar cada archivo generado. No se incluyen conversaciones, tokens o IPs en reportes de IA/auditoría.
- Montos históricos consultados sin recalcular IVA/comisión ni mezclar monedas. Sin DELETE ni modificaciones de historial financiero. Fechas UTC, sin DateTime.Now, y CancellationToken en operaciones async.
- Paquetes justificados: ClosedXML 0.105.1 MIT y QuestPDF 2026.9.1 Community para uso académico; referencias y limitación comercial en REPORTES.md. Herramienta oficial de autoría Power BI usada fuera del backend, sin Node.js en el repositorio.
- `.env.powerbi` ignorado por la regla versionada `.env.*`; cachés/datos/PBIX excluidos por `bi/.gitignore`. Frontend local sigue excluido y no forma parte del diff. No push/merge ni cambios a main/develop locales.

## Pendientes y límites

1. Criterio de salida no cumplido aún: tablero actualizado y funcionando en Power BI Desktop, junto con PBIX local de ejemplo. Usuario realizará esa comprobación con POWER_BI.md; mantener F10-06 y Q11 pendientes hasta confirmarla. CI remoto también requiere publicar el PR.
2. R-14: el total de tiempo global queda null porque la vista agregada no conserva conteo de mediciones no nulas; no se inventa un promedio. R-15 cuenta participaciones diarias de usuarios, no únicos globales.
3. Exportación acotada: 10000 filas XLSX y 500 PDF por sección. Se transmite el archivo como stream, pero ClosedXML conserva el libro en memoria; no es generación ilimitada en memoria constante. Rechazo explícito sin truncamiento ante exceso.
4. Power BI importa manualmente y usa credencial administrativa de reportes independiente de JWT. No hay RLS por dueño, refresh programado, publicación cloud ni servicios/licencias de pago activados. Un PBIX puede contener información privada.

No se detectan otras infracciones de §§5/12 en el alcance de backend revisado. Guías para uso y frontend: REPORTES.md, POWER_BI.md, ENDPOINTS.md y ANALISIS_FRONTEND.md.
