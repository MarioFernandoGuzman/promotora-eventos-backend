# Documentos RAG — F9-02

La API permite a un ADMINISTRADOR habilitado cargar documentos públicos del negocio, revisar el texto extraído, crear versiones y desactivarlas. La carga guarda el texto como PENDIENTE; para que José pueda consultarlo, ejecuta POST /api/v1/ia/documentos/{id}/reindexar. El flujo completo de Fase 9 está en [IA_RAG.md](IA_RAG.md). Esta guía conserva el recorrido de carga/versionado sin consumo cloud de F9-02.

## Arranque local y autenticación

Abre Docker Desktop. En PowerShell, ejecuta cada comando por separado:

```powershell
cd C:\Users\mario\OneDrive\Desktop\backendcomercial
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

Si ya tienes la API abierta, detén **esa instancia** con Ctrl+C antes de reiniciarla para cargar el código nuevo. En otra terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Api
```

Abre [Swagger UI](http://localhost:5080/swagger). En `POST /api/v1/auth/login`, entra con tu administrador configurado localmente. Copia `accessToken`, pulsa **Authorize**, pega el token y confirma. Swagger añade `Bearer`; no dupliques el prefijo. Si el administrador inicial exige cambio de contraseña, completa el flujo de [identidad](IDENTIDAD_SEGURIDAD.md) y vuelve a iniciar sesión antes de usar estos endpoints. No hay usuario/contraseña universal en el repositorio.

La ingesta no necesita llamadas ni claves cloud. Sí necesita PostgreSQL y la configuración habitual de identidad de la API. El script carga el `.env` privado; no copies sus secretos en Swagger salvo la contraseña del login ni los publiques en capturas.

## Prueba en Swagger

1. Crea con tu editor un archivo `reglamento-prueba.md` con este texto de demostración: `Reglamento de prueba: las puertas del evento de demostración abren a las 10:00 UTC. Este documento no describe un evento real.`
2. En `POST /api/v1/ia/documentos`, pulsa **Try it out**. Indica título `Reglamento de prueba` y selecciona el archivo en `archivo`.
3. **Execute** debe devolver **201**, `version: 1`, `estado: PENDIENTE` y un `id`. `Location` apunta al detalle administrativo.
4. Ejecuta `GET /api/v1/ia/documentos/{id}`: verás `documento`, `contenido` y `versiones`. Comprueba que coincide el texto.
5. Ejecuta `GET /api/v1/ia/documentos?page=1&pageSize=20&estado=PENDIENTE`: verás los metadatos sin el texto completo.
6. Cambia el archivo y súbelo por `POST /api/v1/ia/documentos/{id}/versiones`, usando el ID de la última versión. Debe devolver **201**, un ID nuevo y `version: 2`. El detalle de cualquiera de las versiones muestra ambas, descendentes; el contenido anterior permanece intacto.
7. Intentar otra versión desde el ID de versión 1 devuelve **409**. Usa el ID nuevo para continuar.
8. En `POST /api/v1/ia/documentos/{id}/desactivar`, envía `{"estadoEsperado":"PENDIENTE"}`. Devuelve **200**, `estado: INACTIVO`. Repetir devuelve 200 sin duplicar auditoría. Desactiva también la otra versión si deseas retirar todo el ejemplo.

## Contrato para frontend

Todas las rutas siguientes requieren Bearer JWT de ADMINISTRADOR habilitado, con sesión completa. CLIENTE/DUENO_RECINTO reciben 403 y anónimo 401; un token revocado o usuario deshabilitado puede recibir 401. La comprobación se repite en Application dentro de la transacción.

| Método y ruta | Entrada | Resultado |
|---|---|---|
| POST `/api/v1/ia/documentos` | multipart: `titulo`, `archivo` | 201 DocumentoDto + Location |
| GET `/api/v1/ia/documentos` | page, pageSize, estado opcional | 200 página de DocumentoDto |
| GET `/api/v1/ia/documentos/{id}` | ID | 200 documento, contenido, versiones |
| POST `/api/v1/ia/documentos/{id}/versiones` | multipart: `titulo`, `archivo` | 201 nueva fila/versión + Location |
| POST `/api/v1/ia/documentos/{id}/desactivar` | JSON: `estadoEsperado` | 200 DocumentoDto INACTIVO |

No fuerces `Content-Type` al enviar `FormData` desde el navegador: este genera el boundary multipart. No renderices el contenido extraído como HTML sin sanitizar.

`DocumentoDto`: `id`, `titulo`, `tipoDocumento`, `version`, `estado`, `fechaCarga` UTC, `subidoPor`. El detalle añade texto y metadatos de versiones; no devuelve rutas internas ni el binario original. `tipoDocumento` guarda el formato detectado por extracción (`PDF`, `DOCX`, `TXT`, `MD`).

Listados: page 1–100000, pageSize 1–100, default 20; orden fechaCarga e ID descendente. Filtro de estado PENDIENTE, INDEXADO, ERROR o INACTIVO; omitirlo muestra todos. Cada fila es una versión.

Errores con ProblemDetails: 400 campos/filtros/archivo inválidos; 401/403 acceso; 404 ID inexistente; 409 versión obsoleta, referencia histórica no gestionada o estado esperado distinto; 413 solicitud que supera 6 MiB. Un archivo de más de 5 MiB dentro de un multipart menor al límite HTTP devuelve 400. Un error inesperado de persistencia/auditoría devuelve 500 y revierte la carga.

## Almacenamiento, versiones y límites

Se utiliza el esquema Flyway existente sin alterarlo. El contenido extraído se guarda en `documentos_rag.contenido`; no se conserva el binario ni el nombre/ruta suministrados por el cliente. `ruta_archivo` contiene una referencia lógica generada `db:documentos-rag/<UUID>`, compartida por las versiones de la misma serie. No es una URL pública ni un archivo en disco. Esta convención resuelve el agrupamiento sin agregar columnas o usar el título como identidad. Las filas históricas sin esta referencia no se reinterpretan: pueden consultarse/desactivarse, pero crear versiones desde ellas devuelve 409.

La actualización crea una nueva fila y conserva las anteriores. Se bloquea la primera fila de la serie en la transacción y se exige partir de su última versión; dos solicitudes concurrentes basadas en la misma versión producen un 201 y un 409. Crear una versión no desactiva automáticamente la versión anterior: todavía no se activa/indexa el reemplazo. Al reindexar y publicar correctamente la nueva versión, F9-04 retira las anteriores INDEXADO. La desactivación afecta solo al ID indicado y usa UPDATE condicional; no borra documentos ni chunks. La recuperación excluye estados distintos de INDEXADO.

Solo administradores deben cargar contenido público de la organización. El texto se trata como datos, no instrucciones ejecutables. La operación registra INGEST y la desactivación UPDATE en `logs_auditoria`, en la misma transacción; no registra el archivo/texto, secretos o rutas. Si falla auditoría, se revierte la creación.

Límites de implementación: 5 MiB por archivo; un millón de caracteres extraídos; PDF hasta 200 páginas, sin cifrado ni OCR. PDF conserva páginas con el separador `\f`, incluso páginas vacías. DOCX extrae párrafos/tablas del cuerpo principal; no se promete extracción de imágenes, encabezados o notas ni paginación física. TXT/MD exigen UTF-8, con BOM opcional; no se admiten binarios ni controles extraños. El MIME enviado por el cliente no determina la validez: PDF/DOCX deben poder parsearse.

DOCX limita 500 entradas y 20 MiB descomprimidos declarados, rechaza partes `.bin`, documentos con macros y XML con DTD/entidades externas; los XML tienen límite de caracteres. Los parsers no ejecutan macros, JavaScript ni herramientas ni recuperan URLs. Hay comprobaciones de cancelación entre lecturas/páginas/párrafos; los parsers de terceros son síncronos y no garantizan interrupción inmediata dentro de una página ni un límite duro de memoria/CPU. Este alcance no sustituye aislamiento de procesos para documentos hostiles.

## Dependencias y comprobación

PdfPig 0.1.16 (namespace UglyToad.PdfPig) extrae PDF bajo [Apache-2.0](https://www.nuget.org/packages/PdfPig/0.1.16). DocumentFormat.OpenXml 3.5.1 extrae DOCX bajo [MIT](https://www.nuget.org/packages/DocumentFormat.OpenXml/3.5.1). Ambas están únicamente en Infrastructure; TXT/MD usan la biblioteca estándar. No se instala Python, Node u Office ni modelos locales.

Las pruebas nuevas cubren extracción real PDF/DOCX, UTF-8, límites, archivos malformados, ZIP expandido/DTD, cancelación; validadores, permisos de todas las rutas, usuario deshabilitado, historial, desactivación idempotente, concurrencia entre administradores, auditoría transaccional y contrato Swagger. PostgreSQL es temporal mediante Testcontainers. No consumen cuota Gemini/Ollama.

Verificación local del 13/10/2026: build Release sin warnings y format limpio; **507 casos verificados**, 23 nuevos. El primer pase global tuvo 506 aprobados y un fallo de inventario Swagger (esperaba 73, recibió las 78 operaciones correctas). Se actualizó esa aserción manteniendo el conteo exacto. Los cuatro ensamblados sin cambios ya estaban verdes (357 casos) y se repitió íntegro API: **150 aprobados, 0 fallidos/omitidos**. Una ejecución intermedia de API quedó interrumpida sin resumen al cambiar el turno y no se cuenta como evidencia. La verificación no consume cloud ni modifica compras/mensajería. No se afirma que la primera invocación global terminara en verde; el resultado final reúne los cinco ensamblados validados. CI remoto se confirma al publicar el PR.

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test -c Release --no-build
```

```powershell
dotnet format --verify-no-changes --no-restore
```
