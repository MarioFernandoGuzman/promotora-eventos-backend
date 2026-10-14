# IA cloud — Ollama + Gemini, sin instalar modelos

## Estado y arquitectura

F9-01 implementa ILlmPort (Ollama Cloud gpt-oss:120b), IEmbeddingPort (Gemini API gemini-embedding-2) e IVectorSearchPort (PostgreSQL/pgvector). Backend C#/.NET y HttpClient en Infrastructure; Application solo conoce puertos y DTOs. No hay modelo, SDK Python ni servidor Ollama local que instalar.

El esquema conserva vector(1536), sin migraciones. El modelo de embeddings debe ser el mismo para indexar y consultar: cambiarlo requiere reindexar aunque conserve la dimensión. No se mezclan vectores de diferentes proveedores. El adaptador pide 1536 dimensiones y rechaza dimensión distinta, vectores cero y componentes no finitos.

F9-01 no agrega endpoints: ingesta en F9-02–F9-04, respuestas con citas en F9-05/F9-06, datos públicos en F9-07, logs en F9-08 y defensas/evaluación en F9-09–F9-11. La prueba manual de credenciales no declara completo el RAG ni habilita todavía chat en Swagger.

## 1. Crear las dos claves con cuota gratuita

- Chat: [Ollama API keys](https://ollama.com/settings/keys). Usa el plan gratuito de tu cuenta y revisa su cuota en el panel; no contrates un plan de pago para esta prueba.
- Embeddings: [Google AI Studio — API keys](https://aistudio.google.com/api-keys). Crea un proyecto/clave del **Free Tier**, sin vincular una cuenta de facturación ni activar el nivel de pago. Comprueba que el modelo gemini-embedding-2 tiene cuota disponible en ese proyecto. Si tu cuenta/región no dispone de cuota, avisa; no habilites facturación como solución.

Google lista el endpoint estándar de Gemini Embedding 2 como gratuito dentro del Free Tier. La cuota es limitada y puede variar por proyecto; consulta [precios](https://ai.google.dev/gemini-api/docs/pricing) y [límites](https://ai.google.dev/gemini-api/docs/rate-limits). No es uso ilimitado. Google indica que los datos del nivel gratuito pueden usarse para mejorar sus productos: enviar únicamente documentos públicos de la promotora, nunca usuarios, compras o finanzas.

El backend no puede verificar si una clave pertenece a un proyecto gratuito o con facturación. Debes mantener ambos servicios en sus niveles gratuitos. No existen reintentos automáticos, escalamiento de plan, fallback a modelos pagados ni llamadas al arrancar. Un 429 produce un error controlado.

La [tarifa actual de Ollama](https://ollama.com/pricing) describe créditos iniciales para un conjunto de modelos starter; no garantiza que gpt-oss:120b esté incluido en todas las cuentas gratuitas. Antes de probar chat verifica su acceso con créditos incluidos. Si exige comprar créditos, detén esa prueba y avisa para acordar un modelo de chat con Free Tier, sin cambiar el RAG ni activar pagos. La prueba de embeddings de esta guía no llama a Ollama.

## 2. Guardar ambas claves en .env

Entra al proyecto:

```powershell
cd C:\Users\mario\OneDrive\Desktop\backendcomercial
```

Abre el archivo existente, conservando las contraseñas de Docker:

```powershell
notepad .env
```

Agrega estas dos líneas reemplazando los marcadores solo en tu equipo. Guarda como .env, no .env.txt. No compartas las claves en chat, capturas ni frontend.

```dotenv
OLLAMA_API_KEY=REEMPLAZAR_CON_TU_CLAVE_OLLAMA
GEMINI_API_KEY=REEMPLAZAR_CON_TU_CLAVE_GEMINI
```

Verifica que Git lo ignora sin mostrar su contenido:

```powershell
git check-ignore .env
```

Debe responder .env. La API solo lee los nombres permitidos OLLAMA_API_KEY y GEMINI_API_KEY; no expande variables ni ejecuta contenido. Rechaza claves vacías o duplicadas. El agente nunca crea ni inspecciona tu .env real mediante herramientas.

En Development, la configuración externa Ollama:ApiKey/Gemini:ApiKey tiene prioridad sobre OLLAMA_API_KEY/GEMINI_API_KEY del proceso y luego sobre .env de la raíz. Configuración externa incluye user-secrets o variables Ollama__ApiKey/Gemini__ApiKey. En producción no se carga .env: se usan variables externas. Las demás líneas del archivo no configuran automáticamente toda la aplicación .NET.

## 3. Prueba mínima de embeddings (opcional y real)

Primero comprueba que la clave está en un proyecto Free Tier sin facturación. Ejecuta:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-EmbeddingsCloud.ps1
```

El script se verifica con HTTP simulado en Windows PowerShell 5.1 y PowerShell 7. El parámetro ExecutionPolicy se aplica únicamente a esa ejecución del script local revisado; no cambia la política persistente del equipo. Lee GEMINI_API_KEY sin imprimirla, envía un texto público de ejemplo con **una sola solicitud** a embedContent y comprueba 1536 valores válidos. No ejecuta chat, no publica documentos ni escribe BD. No se ejecuta en CI y sí consume una pequeña parte de la cuota de tu cuenta. No se debe repetir en bucle. Salida esperada:

```text
Gemini OK: 1536 dimensiones válidas. Se realizó una solicitud; no se probaron chat ni RAG completo.
```

Si falla, verifica modelo/clave/región/cuota en AI Studio. La salida no muestra cuerpo del proveedor ni credenciales. No uses Batch API de procesamiento diferido: no tiene Free Tier. El backend procesa lotes de hasta 16 textos mediante solicitudes estándar secuenciales, una por texto, con orden estable; no agrega varios textos en un mismo Content para evitar un único vector agregado.

## 4. Habilitar y arrancar la API

### Arranque con todas las credenciales en .env

El usuario autorizó expresamente crear su archivo privado local el 13/10/2026. No se versiona ni se muestran sus valores. Incluye credenciales de Docker, conexión restringida PostgreSQL, JWT, bootstrap, firma QR, RabbitMQ, SMTP local y claves de IA. Las credenciales PostgreSQL/JWT/bootstrap existentes se conservan; la firma QR se generó antes de existir boletos en esa base. Los jobs del Worker se habilitan individualmente mediante sus flags; el archivo inicial los conserva deshabilitados para no procesar compras durante una prueba de IA.

Para cargar el archivo en cada arranque sin volver a escribir contraseñas, usa estos comandos desde la raíz, en terminales independientes:

Infraestructura:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

API:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Api
```

Worker:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Worker
```

El script no ejecuta el contenido de .env: lee una lista permitida de nombres, admite valores entre comillas simples/dobles, rechaza duplicados y variables ajenas al proyecto. Propaga la configuración al proceso hijo, da prioridad a los valores del archivo sobre user-secrets anteriores en este arranque y restaura el entorno al terminar. Las claves de IA se mapean también a Ollama__ApiKey/Gemini__ApiKey. ExecutionPolicy se aplica solo a ese proceso, sin cambiar la política persistente. No uses este script para producción.

Compose lee .env directamente y permite RABBITMQ_USER (default promotora). En un volumen RabbitMQ existente cambiar esa variable no crea ni modifica usuarios: el usuario local debe tener su contraseña configurada y permisos limitados a los exchanges/colas de la aplicación; no necesita rol administrator ni acceso a todo el vhost.

La carga automática incorporada en WebApi sin este script sigue acotada a las claves de IA. Para usar **toda** la configuración de .env debes arrancar con Start-Local.ps1; dotnet run directo solo ve las variables ya presentes en su terminal y sus proveedores de configuración habituales.

### Alternativa: configuración externa por user-secrets

```powershell
 dotnet user-secrets set "IA:Habilitada" "true" --project src/Promotora.WebApi
```

```powershell
 dotnet user-secrets set "Ollama:ChatModel" "gpt-oss:120b" --project src/Promotora.WebApi
```

Conserva conexión PostgreSQL, JWT, bootstrap y claves de boletos de las fases anteriores. Ollama:EmbeddingUrl y Ollama:EmbeddingModel ya no se usan; puedes eliminar esas configuraciones antiguas si las añadiste:

```powershell
 dotnet user-secrets remove "Ollama:EmbeddingUrl" --project src/Promotora.WebApi
```

```powershell
 dotnet user-secrets remove "Ollama:EmbeddingModel" --project src/Promotora.WebApi
```

```powershell
 dotnet run --project src/Promotora.WebApi
```

No necesitas instalar Ollama ni ejecutar ollama pull, ollama list o un servicio en localhost:11434. El arranque valida configuración sin llamar a proveedores: claves incorrectas se detectan en una solicitud real. Worker no necesita estas claves en F9-01. Hasta F9-06 no hay conversación disponible en Swagger.

## Límites y alcance de José

Pregunta hasta 1000 caracteres, contexto público hasta 6000, salida solicitada hasta 512 tokens (Ollama:MaxTokensSalida: 64–1024), think=low, timeout total de 60 segundos (Ollama:TimeoutSegundos: 5–120). Sin historial creciente, reintentos ni herramientas de escritura. Los caracteres no constituyen un contador exacto de tokens; el razonamiento interno del modelo también puede consumir presupuesto. Respuestas truncadas no se aceptan como completas.

Embeddings: 1–16 textos por lote, hasta 6000 bytes UTF-8 por texto, finalidad Documento o Consulta, prefijos de recuperación recomendados por Google. El timeout total Gemini:TimeoutSegundos (5–120, default 60) incluye todo el lote. Si falla una solicitud, no se devuelve un lote parcial ni se reintenta automáticamente. El puerto suma usageMetadata.promptTokenCount cuando está presente en todas las respuestas; si falta en alguna, TokensEntrada=null, nunca cero ficticio ni una suma parcial.

José responde respetuosamente en español sobre información pública de la organización, eventos, recintos, precios y políticas documentadas. Se presenta cuando se solicita; sin información suficiente reconoce que no sabe. La pregunta y el contexto son datos separados de las instrucciones fijas. No accede a usuarios, compras o finanzas. Las citas verificadas, controles HTTP, auditoría y pruebas de inyección se completarán en las tareas restantes; el prompt por sí solo no garantiza la seguridad.

Las claves van exclusivamente a sus respectivos hosts fijos: Bearer a https://ollama.com/api/chat y x-goog-api-key a https://generativelanguage.googleapis.com/...:embedContent. Nunca se ponen en URL ni cuerpos, se bloquean redirecciones y no se registran en logs.

## Verificación automática

Resultado final de la corrección F9-01: **484 pruebas Release aprobadas, 0 fallidas y 0 omitidas**, build sin warnings y formato limpio. El script manual se verificó con HTTP simulado en Windows PowerShell 5.1 y PowerShell 7, sin usar claves reales.

Verificación local autorizada del 13/10/2026: una solicitud real Gemini produjo 1536 dimensiones; una conversación real con gpt-oss:120b terminó con stop (371 tokens de entrada y 63 de salida), usando contexto explícitamente de demostración. La respuesta no constituye una cita RAG validada: F9-05/F9-06 siguen pendientes. Docker saludable, Flyway validate correcto, RabbitMQ autenticado con acceso limitado y API /health/live, /health/ready y Swagger HTTP 200. Start-Local.ps1 se verificó con valores simulados en Windows PowerShell 5.1: propagación, prioridad, restauración del entorno y rechazo de variables ajenas. Ninguna clave real se incorpora a la evidencia ni a Git.

Incidencias de verificación: la primera ejecución no pudo iniciar contenedores porque Docker Desktop estaba apagado; se inició el motor y se repitió la suite. Una ejecución posterior presentó timeout en CompraResilienciaTests.CorteRealDelBrokerAntesODespuesDelCommitReentregaSinDuplicarVenta (despuesCommit=true). Las cinco pruebas de esa clase pasaron aisladas y la suite final completa pasó sin cambios en código de compras ni en esas pruebas. No se determinó la causa del timeout; vigilar su repetición en CI. Las dos solicitudes reales posteriores están registradas en el párrafo anterior; no permiten comprobar la cuota o facturación restante de las cuentas.

F9-02 añade la carga y gestión administrativa de documentos sin consumo cloud. La guía de arranque, autenticación, formulario Swagger, versiones y desactivación está en [IA_DOCUMENTOS.md](IA_DOCUMENTOS.md). La indexación y el chat público siguen pendientes.

Las pruebas usan HTTP simulado y PostgreSQL temporal: **cero llamadas reales** a Ollama o Gemini, sin requerir claves reales ni descargar modelos. Incluyen transporte, claves separadas, dimensiones, finalidad de recuperación, lotes con errores, cuotas, cancelación, configuración .env y pgvector real.

```powershell
 dotnet build -c Release -warnaserror
```

```powershell
 dotnet test --no-build -c Release --report-trx
```

```powershell
 dotnet format --verify-no-changes --no-restore
```

No se agregan paquetes en la corrección cloud. Microsoft.Extensions.Http 10.0.10 (MIT) ya se añadió en F9-01 para administrar conexiones. Referencias: [Ollama Cloud](https://docs.ollama.com/cloud), [Gemini embeddings](https://ai.google.dev/gemini-api/docs/embeddings), [Gemini Embedding 2](https://ai.google.dev/gemini-api/docs/models/gemini-embedding-2).
