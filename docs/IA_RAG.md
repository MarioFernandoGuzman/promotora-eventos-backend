# José y RAG: uso local e integración

El módulo de Fase 9 permite responder preguntas públicas con fuentes verificadas. Ollama Cloud gpt-oss:120b selecciona extractos; Gemini gemini-embedding-2 genera vectores de 1536 dimensiones y PostgreSQL/pgvector recupera fragmentos. No se instalan modelos locales. La respuesta final es extractiva: el servidor publica citas literales del contexto y no prosa libre del modelo sin respaldo.

## Arranque

Abre Docker Desktop. Desde PowerShell ejecuta cada comando por separado:

```powershell
cd C:\Users\mario\OneDrive\Desktop\backendcomercial
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Api
```

La API normal usa http://localhost:5080; abre [Swagger](http://localhost:5080/swagger). Si ya está ejecutándose una instancia anterior, detenla desde su terminal antes de reiniciarla. La verificación del desarrollo usó adicionalmente el puerto temporal 5081; no es un puerto obligatorio de la aplicación.

El `.env` privado debe tener las configuraciones existentes de BD/identidad, `IA__Habilitada='true'`, `OLLAMA_API_KEY` y `GEMINI_API_KEY`. Start-Local carga esas claves para el proceso y no las imprime. No las copies al frontend ni a Git. Mantén las cuentas en cuota gratuita sin habilitar facturación o recargas; el software no puede verificar el nivel de facturación de una cuenta.

RAG funciona con API + PostgreSQL. No necesita Worker ni activar compras. `/health/ready` comprueba BD, no la cuota de los proveedores. La API necesita sus claves al habilitar IA; una cuota agotada se descubre al llamar al proveedor.

## Preparar el conocimiento en tu PC

1. Inicia sesión en Swagger con tu administrador local. Si `debeCambiarPassword` es true, completa primero el cambio inicial: copia `accessToken`, pulsa Authorize y usa `PUT /api/v1/usuarios/{usuarioId}/password` con `passwordActual` y `passwordNuevo`. Vuelve a iniciar sesión y actualiza Authorize. El ID viene en el login. No se cambia esa contraseña automáticamente ni se omite la comprobación.
2. Sube `docs/rag/REGLAMENTO.md` con `POST /api/v1/ia/documentos`, campos `titulo` y `archivo`. Devuelve 201 y un documento PENDIENTE. La guía de archivos/versiones está en [IA_DOCUMENTOS.md](IA_DOCUMENTOS.md).
3. Ejecuta `POST /api/v1/ia/documentos/{id}/reindexar` sin body. Debe devolver 200 e INDEXADO. Este paso consume embeddings; no lo repitas si ya funciona.
4. Repite la carga/indexación, uno por uno, con FAQ.md, REEMBOLSOS.md y CARRERAS.md cuando quieras disponer de esos temas. No subas el README técnico.
5. Ejecuta `POST /api/v1/ia/consultas` sin autenticación con `{"pregunta":"¿Las localidades del evento tienen asientos numerados?"}`. Debe devolver una cita del reglamento.

Estos documentos no se cargan automáticamente en tu BD. En la revisión local el administrador seguía con cambio inicial obligatorio: debes completarlo antes de la ingesta. No se modificaron sus credenciales para la evaluación. Sin documentos INDEXADO ni catálogo relevante, SIN_CONTEXTO es el resultado correcto.

## Contrato de chat para frontend

```json
{
  "pregunta": "¿Las localidades del evento tienen asientos numerados?",
  "sessionId": null
}
```

La primera respuesta genera un UUID. Envía ese `sessionId` en mensajes posteriores para agrupar logs. No es autenticación ni acceso al historial; cada pregunta debe incluir el contexto necesario del evento. No hay streaming ni memoria conversacional de mensajes previos. Muestra un indicador mientras esperas y no reintentes automáticamente.

Una respuesta OK contiene `sessionId`, `respuesta`, `fuentes`, `estado`, `modelo`, `tokensEntrada` y `tokensSalida`. Cada fuente contiene `id`, `documento`, `pagina`, `chunkId`, `documentoId`, `url` y `extracto`. La página/chunk/documentoId pueden ser null en fuentes del catálogo. Las fuentes RAG no tienen URL pública al original: el detalle documental requiere administrador. Muestra el título, la página disponible y el extracto recibido en un panel o desplegable. Para fuentes del catálogo, `url` es la ruta relativa del detalle público de API; el frontend puede llevar al usuario a su propia pantalla de ese evento.

| Respuesta | Comportamiento de pantalla |
|---|---|
| 200 / OK | Mostrar respuesta literal y fuentes; permitir expandir extractos |
| 200 / SIN_CONTEXTO | Mostrar que faltan fuentes; sugerir precisar evento o contactar a la organización |
| 200 / RECHAZADA | Mostrar explicación respetuosa de alcance; no insistir con la misma solicitud |
| 400 | Validar pregunta 1–1000 caracteres y sessionId UUID no vacío |
| 413 | Cuerpo HTTP demasiado grande; límite de 8 KiB |
| 422 | IA deshabilitada o fallo de proveedor; mensaje de indisponibilidad |
| 429 | Límite/cuota/capacidad; respetar Retry-After si existe y permitir reintento manual posterior |
| 500 | Error inesperado, incluida persistencia de log; mostrar traceId para soporte |

Renderiza texto, no HTML generado. No conviertas URLs arbitrarias dentro del texto en enlaces ejecutables. No mandes documentos privados, datos de pago o credenciales al chat. El agente puede orientar con reglas públicas, pero no consulta operaciones personales ni realiza acciones.

## Administración del índice

La carga y la indexación son explícitas y separadas para controlar consumo. Los tipos admitidos son PDF con texto, DOCX, TXT y MD. La extracción tiene límite de 5 MiB, un millón de caracteres y 200 páginas PDF. No hay OCR. DOCX no determina página física.

El chunking utiliza hasta 3200 unidades Unicode como aproximación a ~800 tokens, con solapamiento de un octavo (~100 tokens). No utiliza el tokenizer exacto del modelo; también limita cada fragmento a 5500 bytes UTF-8. Cada documento indexado admite hasta 32 fragmentos, procesados en lotes de 16. Divide documentos mayores en unidades públicas más pequeñas.

La indexación conserva orden, página y modelo del vector en metadata. La búsqueda solo lee documentos INDEXADO del modelo vigente. Cambiar de modelo exige reindexar incluso si conserva la dimensión. Los vectores históricos sin modelo compatible no participan en el chat de producción.

La publicación se realiza en una transacción y sustituye el índice del documento de manera completa. Un bloqueo PostgreSQL por documento evita indexaciones simultáneas del mismo ID entre instancias. Las llamadas cloud ocurren fuera de transacciones. Antes de publicar se vuelve a verificar estado y última versión. No se publica una versión obsoleta o desactivada mientras se generaban vectores.

Al publicar una versión nueva se retiran las anteriores INDEXADO de su serie. Si falla la reindexación de un documento ya INDEXADO, conserva su índice previo; si falla desde PENDIENTE, pasa condicionalmente a ERROR. Se puede reintentar con la misma ruta. No hay reintentos automáticos ni compras de créditos. Cancelar una solicitud antes de publicar conserva el estado anterior y permite reintentar; no hay un job oculto de indexación.

## Límites y observabilidad

Por defecto hay cinco consultas por minuto/IP y dos consultas simultáneas por proceso. El límite por IP no es una cuota global distribuida: detrás de un proxy debe configurarse una política de IP/proxy confiable antes de exponerlo públicamente. No se aceptan herramientas de escritura, SQL ni acceso a tablas de usuarios, compras o finanzas por parte del agente.

Configuración opcional externa/user-secrets: `Rag:TopK` (default 3, rango 1–5), `Rag:SimilitudMinima` (default 0.65, rango 0–1), `RateLimiting:IaPermitLimit` (default 5, rango 1–60), y los tiempos/límites de Ollama/Gemini de [IA_OLLAMA.md](IA_OLLAMA.md). No añadas nombres arbitrarios al `.env`: Start-Local valida una lista permitida; para estos ajustes opcionales usa user-secrets o configuración externa del host.

Las consultas procesadas se registran en logs_agente_ia: sesión, consulta pública, respuesta, fuentes, duración, modelo, tokens y estado. Texto con credenciales aparentes o rechazado se omite. El endpoint público no vincula identidad de usuario. Validación/rate limit previos al caso de uso quedan en logs HTTP sin cuerpo. Los logs son solo INSERT; una caída de BD impide entregar una respuesta sin log. En desconexiones/cancelaciones el registro es de mejor esfuerzo con presupuesto de tres segundos.

Los filtros de alcance/inyección/credenciales son capas defensivas y pueden producir falsos positivos; no son clasificación semántica perfecta ni DLP infalible. El aislamiento por puertos y la salida extractiva reducen el riesgo: cualquier hecho mostrado debe estar literalmente en una fuente pública recuperada. La exactitud del documento publicado sigue siendo responsabilidad del administrador.

## Evaluación acotada

Preguntas sugeridas: localidades sin asientos numerados; validación QR; requisitos de carrera; alcance del reembolso simulado; evento no publicado; solicitud de operaciones personales; intento de revelar instrucciones. Las pruebas automáticas usan proveedores fake y PostgreSQL real temporal, sin cuota cloud.

Evaluación real del 13/10/2026: dos llamadas estándar Gemini (fragmento y pregunta) produjeron 1536 dimensiones, similitud coseno 0.8122, superior al umbral 0.65. Una llamada Ollama gpt-oss:120b, con el prompt del código y contexto público controlado, produjo extractos literales válidos sobre localidades sin asientos numerados; terminó con stop, 514 tokens de entrada y 100 de salida. No hubo reintentos. Esta evaluación verifica los proveedores y formato de citas; el flujo completo HTTP/BD/log se verificó por separado con proveedores fake en PostgreSQL temporal. No se presenta como ingesta real en la BD del usuario.
