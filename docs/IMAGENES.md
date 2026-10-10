# Imágenes de planos, eventos y presentadores (F4-09)

El almacenamiento local se implementa en Infrastructure detrás de `IImagenStorage`, definido en Application. Se reutilizan `recintos.imagen_plano_url`, `eventos.imagen_url` y `presentadores.imagen_url`; no se crean tablas ni migraciones. Cada cambio de URL y su UPDATE de auditoría se confirman juntos. El Worker no cambia.

## Contratos y permisos

| Método/ruta | Permiso |
|---|---|
| POST `/api/v1/recintos/{id}/imagen-plano` | Dueño del recinto o administrador; recinto activo |
| POST `/api/v1/eventos/{id}/imagen` | Dueño del recinto o administrador; evento BORRADOR/PROGRAMADO sin compras y recinto activo |
| POST `/api/v1/presentadores/{id}/imagen` | Administrador con sesión completa; presentador activo |
| GET `/api/v1/imagenes/{nombre}` | Anónimo, solo si el archivo está referenciado por un recurso público |

Las subidas requieren `multipart/form-data`, campo **archivo** de tipo archivo, Bearer y sesión completa (cambio obligatorio terminado). Éxito 200:

```json
{"url":"/api/v1/imagenes/0123456789abcdef0123456789abcdef.png"}
```

La URL es relativa al origen de la API. Frontend debe combinarla con su URL base de backend; no con el origen del frontend. Se obtiene también desde el detalle, catálogo o mapa correspondientes. No enviar rutas locales del computador ni configurar un Content-Type manual al usar FormData: el navegador añade el boundary.

```javascript
const form = new FormData();
form.append('archivo', file);
const response = await fetch(`${apiBase}/api/v1/eventos/${eventoId}/imagen`, {
  method: 'POST', headers: { Authorization: `Bearer ${accessToken}` }, body: form
});
```

Este ejemplo es del frontend; el backend permanece .NET/C#.

## Validación y publicación

Solo PNG/JPEG con MIME coherente con el formato decodificado, archivo entre 1 byte y 5 MiB, lados hasta 4096 píxeles y hasta 16 millones de píxeles. Se valida longitud real al leer el stream; no basta la longitud declarada. Se exige decodificación completa y se rechazan imágenes animadas o truncadas. SVG/GIF/WebP y archivos falsos se rechazan. El cuerpo multipart tiene límite de transporte de 6 MiB.

SkiaSharp decodifica a píxeles y reexporta PNG, descartando metadatos y contenido adjunto. El PNG resultante debe caber en 10 MiB; no se guarda el original. Nombre generado UUID sin guiones, extensión .png; el nombre enviado por el usuario no participa en rutas de almacenamiento. La reexportación no preserva metadatos EXIF, incluyendo orientación; preparar la foto con orientación correcta antes de subir.

Errores de archivo/cuerpo/campos → 400; sin token → 401; rol o propiedad ajena → 403; recurso inexistente → 404; inactivo o evento no configurable → 409. Un rechazo del límite de transporte puede devolver 413 antes de procesar el archivo. Errores HTTP usan ProblemDetails con traceId; el controller no usa DbContext ni escribe archivos.

GET responde image/png, `X-Content-Type-Options: nosniff`, `Cache-Control: no-store` y CSP restrictiva. Solo planos de recintos activos, imágenes de eventos visibles del catálogo (PROGRAMADO/EN_VENTA/AGOTADO con recinto/categoría activos) e imágenes de presentadores activos asociados a dichos eventos. Una imagen de borrador o presentador sin evento público devuelve 404, incluso con sesión: esta ruta es para contenido público. URL antigua tras reemplazo o recurso ocultado → 404 salvo que otro recurso público todavía la referencie. No se expone el directorio como archivos estáticos.

## Almacenamiento y consistencia

Por defecto: `src/Promotora.WebApi/uploads/`, ignorado por Git. Configuración opcional no sensible `Images:RootPath` o `Images__RootPath`; en producción debe apuntar a almacenamiento persistente accesible solo por el proceso de la API. La ruta configurada es del servidor, nunca del cliente. No commitear los archivos subidos.

El puerto permite reemplazar el almacenamiento local en el futuro. La escritura genera un archivo único, sin sobreescribir. BD y filesystem no comparten una transacción distribuida: ante fallo/cancelación se elimina solo el archivo nuevo no confirmado; reintentos de BD reutilizan ese archivo. Una caída abrupta entre escribir y confirmar puede dejar un archivo huérfano. Los reemplazos conservan físicamente las imágenes anteriores; no son públicas cuando dejan de estar referenciadas. La limpieza automática/distribución en varias réplicas no forma parte de F4-09. Incluir uploads y BD en respaldo y controlar espacio disponible; no borrar archivos en uso.

## Prueba local para desarrolladores

Requisitos y secretos en [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md), autenticación en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). Reiniciar una API abierta para cargar los endpoints. Ejecutar por separado:

```powershell
dotnet restore
```

```powershell
dotnet run --project src/Promotora.WebApi
```

1. Abrir [Swagger](http://localhost:5080/swagger), login y Authorize con solo accessToken.
2. Crear recinto propio y evento BORRADOR; como administrador, crear un presentador.
3. Abrir cada POST de imagen → Try it out → indicar id → Choose File en archivo → seleccionar PNG/JPEG pequeño → Execute. Guardar url de la respuesta y comprobar el campo URL del recurso.
4. Abrir URL de plano con el origen `http://localhost:5080`; debe verse la imagen. La imagen del borrador devuelve 404.
5. Asociar presentador al evento y pasar a PROGRAMADO; ahora imágenes de evento/presentador son públicas. El mapa utiliza imagenPlanoUrl; su geometría conserva coordenadas relativas al tamaño del plano.
6. Como otro dueño/cliente, intentar subir → 403. Probar SVG, MIME incoherente, archivo vacío, truncado o grande → rechazo sin nueva URL ni auditoría. Reemplazar plano → URL nueva, anterior 404. Ocultar el recurso → su imagen deja de publicarse.

Las pruebas automatizadas usan PostgreSQL temporal y directorios independientes, sin modificar imágenes locales. Se cubren normalización PNG/JPEG, MIME, tamaño, dimensiones, traversal, permisos, publicación y compensación del archivo si la BD rechaza la asignación.
