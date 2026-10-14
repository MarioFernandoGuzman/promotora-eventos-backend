# Cierre de Fase 9 — Agente público y RAG

F9-01–F9-11 completas en la rama de trabajo. Backend C#/.NET, arquitectura por puertos, PostgreSQL/pgvector, Ollama Cloud y Gemini API; sin servidor/modelos locales. Reportes, exportaciones y Power BI no se implementan en este cierre, por instrucción del usuario. Tampoco se crea aún el frontend.

## Evidencia

- Build Release con -warnaserror: 0 warnings, 0 errores.
- dotnet format --verify-no-changes --no-restore: limpio.
- Una suite completa de cierre: **524 aprobadas, 0 fallidas, 0 omitidas**, 2 min 46 s. Incluye arquitectura, API, aplicación, dominio e infraestructura; 17 casos añadidos desde el cierre F9-02. Se hicieron previamente pruebas focalizadas de cada cambio, sin repetir suites completas por tarea.
- Dos escenarios API integrales con PostgreSQL temporal y proveedores fake: carga/indexación/consulta/citas/log y conservación de índice ante fallo; permisos administrativos, validación, rechazo de inyección y rate limit sin consumir proveedor.
- Pruebas acotadas de Unicode/páginas/solapamiento, cita literal/fuente existente, filtro vectorial de modelo/estado, publicación transaccional/rollback, lectura pública e inmutabilidad de logs.
- API ejecutada desde el .env privado, /health/ready Healthy y Swagger con **80 operaciones REST**. El inventario se generó desde ese Swagger en docs/ENDPOINTS.md. No se exponen secretos en la evidencia.
- Evaluación real mínima: dos embeddings de texto público/pregunta (1536 dimensiones, coseno 0.8122) y una llamada gpt-oss:120b con el prompt vigente. Selección JSON con cita literal válida sobre localidades sin asientos numerados; stop, 514 tokens de entrada y 100 de salida. Sin reintentos, modelos descargados o pagos. El flujo HTTP/BD completo se verificó por separado con fake; no se afirma una ingesta real en la BD del usuario.

## Criterio de salida

| Requisito | Implementación y comprobación |
|---|---|
| Ingesta pública administrada | Carga/extracción de PDF/DOCX/TXT/MD, versiones preservadas y desactivación; solo administrador habilitado |
| Indexación | Fragmentos con solapamiento/página/orden y embeddings por lotes; publicación atómica, modelo en metadata y reindexación explícita |
| Recuperación | Coseno, top-k, umbral, documentos INDEXADO y modelo compatible; contexto acotado |
| Respuesta con fuentes | Modelo selecciona extractos; servidor valida fuente y coincidencia literal. Devuelve documento/página/chunk y extracto |
| No inventar | SIN_CONTEXTO cuando faltan fuentes o la salida no valida; no se publica prosa libre generada |
| Solo público | Puerto SQL acotado a catálogo en venta/recintos/precios; sin acceso del agente a usuarios/compras/finanzas |
| Defensa | Límites de HTTP/texto, frecuencia/IP y concurrencia; rechazo de operaciones privadas/inyección y credenciales aparentes |
| Trazabilidad | Logs INSERT-only con sesión, pregunta pública, respuesta, fuentes, tiempo, modelo, tokens y estado; redacción de contenido sensible |
| Conocimiento inicial | FAQ, reglamento, reembolsos y guía de carreras revisados contra reglas implementadas; carga voluntaria, sin políticas inventadas |

## Revisión frente a AGENTS.md §§5 y 12

No se detectaron incumplimientos en el diff de Fase 9 revisado: no cambia cupo, ledger, compras ni mensajería; no borra usuarios/finanzas/logs; los DELETE añadidos afectan únicamente chunks reemplazados dentro de su transacción. Los documentos y versiones se conservan. Las transiciones de estado son condicionales; publicación comprueba versión/estado y la ingesta se audita. Fechas UTC y CancellationToken. Controllers sin DbContext, SQL o proveedores directos. Application no referencia Infrastructure, no hay cambios de esquema ni migraciones editadas y los secretos están fuera de Git.

La revisión incluye la ruta de reindexación expresamente aprobada. El diccionario, AGENTS y guías distinguen carga PENDIENTE de publicación INDEXADO; la reindexación no se ejecuta automáticamente al arrancar. Las licencias de dependencias incorporadas están documentadas: Microsoft.Extensions.Http MIT, PdfPig Apache-2.0 y DocumentFormat.OpenXml MIT.

## Límites y siguientes pasos

- El administrador local mantiene el cambio inicial obligatorio. Debe completar ese paso y cargar/indexar documentos antes de probar RAG contra su corpus local; no se alteraron sus credenciales ni se pobló su BD mediante privilegios alternativos.
- Generación extractiva y chat sin memoria de mensajes previos: sessionId agrupa logs, no es acceso al historial. El contenido oficial debe ser revisado por la organización.
- TopK 3 y umbral 0.65 configurables; la única evaluación real descrita no equivale a una evaluación estadística amplia de calidad. No se promete clasificación perfecta de alcance o detección infalible de datos sensibles.
- Límites: 32 fragmentos por documento, 16 por lote, sin OCR/paginación física DOCX; parsers síncronos sin aislamiento de proceso. Cuotas de proveedor y capacidad del host siguen siendo límites reales.
- Rate limit por IP y concurrencia por proceso; configurar proxy confiable y endurecimiento de despliegue antes de exponer públicamente. No se cambia automáticamente a servicios de pago.
- Pasarela financiera y correo de desarrollo conservan su alcance previo. Worker no se activó para esta validación de IA.
- CI remoto se confirma al publicar el PR. Manual e inventario para integrar web: ANALISIS_FRONTEND.md y ENDPOINTS.md. Reportes/BI siguen pendientes; D-09 aún requiere elección del usuario.
