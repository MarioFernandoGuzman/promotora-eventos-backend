# IA con Ollama — configuración y alcance

## Estado por tarea

F9-01 implementa puertos, chat Ollama Cloud, embeddings Ollama local y recuperación pgvector sobre el esquema existente. No agrega endpoints todavía. La ingesta se incorpora desde F9-02 y el chat público con citas desde F9-05/F9-06; no se puede probar una conversación en Swagger hasta completar esas tareas. Activar la configuración por sí sola no consume tokens ni descarga modelos.

Backend exclusivamente C#/.NET. Application define ILlmPort, IEmbeddingPort e IVectorSearchPort; Infrastructure usa HttpClient y EF/pgvector. WebApi registra los adaptadores cuando IA:Habilitada=true. Worker no necesita la clave en este bloque. No se ejecuta Python ni se agrega SDK Python.

## 1. Preparar los embeddings locales

Instala [Ollama para Windows](https://ollama.com/download/windows) y abre la aplicación. Si acabas de instalarlo, abre una nueva ventana de PowerShell para disponer de su comando. El servidor local debe estar disponible en http://localhost:11434.

Descarga el modelo de embeddings (aproximadamente 2,5 GB; requiere RAM y procesamiento locales):

```powershell
ollama pull qwen3-embedding:4b
```

Comprueba que aparece instalado:

```powershell
ollama list
```

No descargues gpt-oss:120b para este proyecto: las respuestas se generan en Ollama Cloud. Solo los embeddings se ejecutan en tu equipo. El adaptador solicita 1536 dimensiones y rechaza respuestas de dimensión distinta, valores no finitos o vectores cero; nunca rellena vectores artificialmente. Modelo y dimensión deben ser los mismos para indexar y consultar. Si cambias modelo, debes reindexar aunque la dimensión sea igual.

## 2. Guardar la clave local

Crea una clave en [Ollama — API keys](https://ollama.com/settings/keys). No la compartas en el chat ni la incluyas en frontend, capturas o commits.

Entra al repositorio:

```powershell
cd C:\Users\mario\OneDrive\Desktop\backendcomercial
```

Abre el `.env` de la raíz (si ya contiene contraseñas de Docker, conserva esas líneas):

```powershell
notepad .env
```

Añade esta línea reemplazando el marcador por tu clave real y guarda el archivo como `.env`, sin extensión `.txt`:

```dotenv
OLLAMA_API_KEY=REEMPLAZAR_SOLO_EN_TU_EQUIPO
```

Comprueba que Git lo ignora, sin mostrar su contenido:

```powershell
git check-ignore .env
```

Debe responder `.env`. El agente no crea, lee mediante herramientas ni escribe tu clave real. La API carga únicamente OLLAMA_API_KEY del archivo, solo en Development y con IA habilitada. No expande variables, no ejecuta contenido y rechaza clave vacía/duplicada. Otras líneas se ignoran; esto no es un cargador dotenv general para toda la configuración .NET.

Prioridad de la clave: configuración externa Ollama:ApiKey (incluye user-secrets o Ollama__ApiKey), luego OLLAMA_API_KEY del proceso y finalmente `.env` de la raíz del repositorio. El `.env` no se carga en producción; usa variables de entorno allí. La ejecución estándar mantiene src/Promotora.WebApi como content root; el archivo se busca dos niveles arriba, sin búsqueda por directorios arbitrarios.

## 3. Habilitar y arrancar

Configura la activación (sin guardar secretos en archivos versionados):

```powershell
dotnet user-secrets set "IA:Habilitada" "true" --project src/Promotora.WebApi
```

Configura el modelo de chat:

```powershell
dotnet user-secrets set "Ollama:ChatModel" "gpt-oss:120b" --project src/Promotora.WebApi
```

Configura el modelo de embeddings:

```powershell
dotnet user-secrets set "Ollama:EmbeddingModel" "qwen3-embedding:4b" --project src/Promotora.WebApi
```

Configura su dirección local:

```powershell
dotnet user-secrets set "Ollama:EmbeddingUrl" "http://localhost:11434/" --project src/Promotora.WebApi
```

Mantén la conexión PostgreSQL, JWT, bootstrap y claves de boletos configurados en las fases anteriores. Arranca la API:

```powershell
dotnet run --project src/Promotora.WebApi
```

Usa la URL de Swagger que corresponda a tu perfil de lanzamiento. F9-01 permite validar configuración/arranque; aún no expone chat ni ingesta. El arranque no llama a Ollama para comprobar la clave: una clave incorrecta solo se descubre al realizar una solicitud real más adelante.

## 4. Consumo y reglas de José

La cuota de Ollama Cloud depende de tu cuenta; no se promete uso gratuito ilimitado. Consulta [uso y planes](https://docs.ollama.com/cloud). En este bloque las pruebas automáticas hacen **cero llamadas reales**: usan un HttpMessageHandler simulado y PostgreSQL temporal para pgvector. No requieren clave real ni el modelo instalado.

Configuración inicial: pregunta hasta 1000 caracteres, contexto público hasta 6000, salida solicitada hasta 512 tokens (Ollama:MaxTokensSalida, rango 64–1024), think=low para gpt-oss, timeout total de 60 segundos (Ollama:TimeoutSegundos, rango 5–120), sin historial creciente y sin reintentos automáticos. Los caracteres limitan tamaño de entrada, no constituyen un contador exacto de tokens. El razonamiento del modelo también puede consumir presupuesto; no se muestra al usuario. Una respuesta truncada no se acepta como respuesta completa. Un 429 devuelve un error controlado sin insistir y consumir más cuota.

José se presenta brevemente cuando el caso de uso lo solicite, responde con respeto en español y solo sobre información pública de la organización, eventos, precios, recintos y políticas documentadas. La pregunta y los documentos se envían como datos separados del system prompt fijo. No dispone de herramientas de escritura ni acceso a compras, usuarios o finanzas. Ante preguntas fuera de alcance explica su función; sin contexto suficiente reconoce que no sabe. El prompt es una defensa parcial: la recuperación, validación de citas, controles HTTP y pruebas de inyección se completan en F9-05–F9-09.

No se guardan cabeceras, claves ni cuerpos del proveedor en logs de infraestructura. Se bloquean redirecciones HTTP para no reenviar credenciales. La clave cloud solo se adjunta a https://ollama.com/api/chat; el cliente local de embeddings no recibe Authorization. Las consultas/respuestas del agente se registrarán en la tabla prevista al implementar F9-08.

## Pruebas y dependencia

Evidencia F9-01: 473 pruebas Release aprobadas, 0 fallidas y 0 omitidas; 19 casos nuevos. Build sin warnings y formato limpio. Se prueban cabeceras/destinos, delimitación y límites del request, errores sin filtrar cuerpos ni repetir llamadas, respuestas truncadas, cancelación, lote/dimensión de embeddings, configuración .env y búsqueda real por coseno con filtros/umbral/limitación/citas. No se verificó una respuesta real de Ollama ni la capacidad del hardware local: las pruebas no consumen cuota y no requieren instalación del modelo.

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Microsoft.Extensions.Http 10.0.10 proporciona IHttpClientFactory y administración de conexiones; licencia MIT, [paquete oficial](https://www.nuget.org/packages/Microsoft.Extensions.Http/10.0.10). No se agrega un SDK de Ollama ni se cambian migraciones. Documentación del proveedor: [chat](https://docs.ollama.com/api/chat), [embeddings](https://docs.ollama.com/api/embed), [modelo local](https://ollama.com/library/qwen3-embedding:4b).
