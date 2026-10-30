# Cierre EXT-NUM — localidades numeradas

Fecha: 29/10/2026. Rama feature/localidades-numeradas basada en la documentación aprobada, por autorización para avanzar mientras su PR permanece en revisión. Sin push, merge ni modificación de main/develop.

## Resultado

Implementadas EXT-NUM-01–07: esquema por evento/localidad, configuración BORRADOR/publicación inmutable, consulta pública paginada, bloqueo operativo, compra general/numerada mixta, reserva indivisible, boletos y QR individuales, ubicación histórica y liberación idempotente. Mesas COMPLETA o POR_SILLA; mismo tipo/precio por mesa. Importe como suma de sillas con D-15 sin cambios.

V12 añade cuatro tablas (31 tablas/235 columnas). V13 refuerza referencias históricas y exige correspondencia de cada boleto numerado con su asignación; V12 ya estaba commiteada y no se editó. Se conserva la semántica de idempotencia anterior. No se convierten compras/eventos con historial ni se reasignan etiquetas publicadas.

## Evidencia

- Build Release con `-warnaserror`: cero warnings/errores.
- Suite completa definitiva: **528 aprobadas, cero fallidas/omitidas**, 3 min 26.057 s. Incluye arquitectura y la regresión general. Log local no versionado: TEMP/promotora-numeradas-final.log.
- Después de la revisión final del orden de bloqueos, se repitió solo el caso integral numerado afectado: aprobado, 23.801 s. El build final también pasó. No se repitieron las otras 527 pruebas sin necesidad.
- `dotnet format --no-restore` y revisión de espacios del diff; verificación final de formato antes del commit.
- Infraestructura focalizada: 11 aprobadas; metadatos reales PostgreSQL contra EF, publicación inmutable, permisos sin DELETE, cupo inconsistente y reserva sin asignación rechazados con rollback.
- Domain: dos casos nuevos para mesa completa/individual, duplicados, pertenencia y ocupación.
- Caso HTTP/PG integrado: propiedad 404, CLIENTE 403, borrador oculto, publicación, paginación, mesa bloqueada íntegra, compra mixta con rollback, dos solicitudes concurrentes (202/409), clave repetida, cancelación repetida, rechazo, carrera procesar/expirar/cancelar, emisión repetida, tres QR distintos, ubicación, escaneo único, reembolso total repetido con un boleto USADO y otros válidos. Ledger final suma cero; la silla usada no se libera.
- V13 también rechaza manipular el tipo del detalle que tiene asignaciones y agregar un boleto numerado sin asignación.
- Herramienta frontend exclusivamente local: build y dos pruebas UI del selector aprobadas; selector de mesa/sillas, asientoIds, conflictos y ubicación. No hay archivos frontend en Git.
- PostgreSQL local: Flyway aplicó V12/V13 desde v11 y validó las 14 migraciones (incluida repetible); 31 tablas. No DROP, borrado de datos ni reinicio de volúmenes.
- API Release temporal en 127.0.0.1:5081: `/health/ready` Healthy; Swagger **86 operaciones**; distribución y asientoIds presentes. La instancia temporal se detuvo; la instancia habitual del usuario no se reinició automáticamente.
- Sin llamadas cloud a IA ni pasarela externa. El pago de pruebas es simulado en PostgreSQL temporal. No se activaron consumidores ni se procesaron compras de la base local.

La primera suite tuvo una regresión en el formato del JSON de asistentes al releer un boleto recién insertado. Se corrigió conservando la respuesta original y añadiendo ubicación por separado; la suite definitiva anterior incluye esa corrección. No se relajaron assertions ni se omitieron pruebas.

## Revisión contra AGENTS §§5 y 12

| Regla | Resultado |
|---|---|
| Sobreventa | UPDATE condicional, índice único parcial de asignación vigente y reserva conjunta con cupo; conflicto revierte todo |
| Idempotencia | Compra conserva clave original; consumidor repetido no emite ni cobra otra vez; liberar verifica asignación y transición |
| Ledger | Pago/ledger/saldos/boletos/confirmación juntos; reembolso revierte importes congelados; no DELETE ni ajustes de historia |
| Outbox | Mismo outbox/transacción, RabbitMQ recibe publicId; posiciones en PostgreSQL y asistentes conservan orden |
| Ownership | Application comprueba actor habilitado y dueño; ajena/inexistente 404; disponibilidad pública sin identidad de comprador |
| QR | HMAC existente; token por persona, VALIDO/USADO/ANULADO; ubicación obtenida del servidor, no del texto del cliente |
| UTC/auditoría | IClock para fechas de operación, TIMESTAMPTZ, CancellationToken; configuración y bloqueos auditados |
| Clean Architecture | Controllers sin SQL/DbContext/proveedores; puertos en Application, adaptadores en Infrastructure; arquitectura en verde |
| Flyway | Solo nuevas V12/V13, ninguna migración histórica editada; sin cambios a bi |
| Secretos | .env ignorado y configuración externa; sin credenciales, QR reales, binarios ni frontend en commits |

Sin incumplimientos detectados en el alcance revisado. Limitaciones explícitas siguientes no se presentan como funcionalidades implementadas.

## Límites y pasos del usuario

- Reiniciar API y Worker con esta versión antes de vender numeradas; una instancia antigua no carga cambios sola. No usar un Worker anterior con compras numeradas.
- Configurar el primer evento por Swagger con [GUIA_NUMERACION.md](GUIA_NUMERACION.md). No se crearon eventos ni posiciones ficticias en la base del usuario.
- Las operaciones toman distribuciones del evento en orden estable; pueden serializarse. La prueba histórica de 500/1000 compradores corresponde a generales, no es benchmark de numeradas.
- Sin editor gráfico administrativo de producción, plantillas reutilizables, tarifas simultáneas para una silla, retención al clic ni WebSocket. Configuración inicial por Swagger; herramienta local cliente de pruebas excluida de Git.
- El frontend local actualizado y Start-Frontend.ps1 quedan excluidos por .git/info/exclude, y el cambio previo de .gitignore del usuario queda sin commit. D-09 y Fase 11 siguen pendientes.
- Reembolso parcial y pasarela real siguen fuera de alcance; no se prometen transferencias externas. Una mesa completa con silla USADO no puede revenderse íntegra después de un reembolso mixto.
- Documentos RAG actualizados en disco: subir nuevas versiones y reindexar manualmente. No se modificó el índice del usuario ni consumió cuota cloud.
- Reportes/Power BI pendientes. Esperar CI remoto verde y que el usuario integre el PR documental antes del merge de este backend. El agente no realiza push ni merge.
