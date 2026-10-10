# Catálogo y eventos — Fase 4

Preparar la API según [Desarrollo local](DESARROLLO_LOCAL.md) y autenticarse según [Swagger para frontend](FRONTEND_SWAGGER_AUTENTICACION.md). Los ejemplos contienen datos de prueba, sin credenciales. Cada tarea mantiene handlers, puertos, mapeo manual y auditoría transaccional.

## F4-04: categorías y presentadores

Rutas: `/api/v1/categorias` y `/api/v1/presentadores`. En ambas, POST crea, GET lista con page/pageSize, GET `/{id}` consulta, PUT `/{id}` reemplaza los campos y DELETE `/{id}` da de baja lógicamente. Éxito: HTTP 200. Las escrituras exigen ADMINISTRADOR activo con sesión completa; consultas exigen sesión completa y admiten los tres roles.

Listados privados incluyen inactivos; page 1–100000, pageSize 1–100. Los nombres de categorías siguen siendo únicos después de su baja, con comparación de PostgreSQL y sin imponer unicidad por mayúsculas/minúsculas distinta del esquema. Los presentadores no tienen restricción de nombre único. No se implementa reactivación. Edición de inactivo → 409; baja repetida → 200 sin auditoría duplicada.

Crear o editar categoría:

```json
{"nombre":"Festival","descripcion":"Eventos de festival"}
```

Nombre obligatorio hasta 50 caracteres; descripción nullable hasta 255. Nombre duplicado → 409.

Crear o editar presentador:

```json
{"nombre":"Artista de prueba","tipo":"ARTISTA","biografia":null,"imagenUrl":null}
```

Nombre obligatorio hasta 150 caracteres; tipo ARTISTA/EQUIPO/PRESENTADOR/ORGANIZACION; biografía nullable y URL nullable hasta 255. Subida de archivos corresponde a F4-09.

Para probar en Swagger: login como administrador → Authorize → crear ambos recursos → consultar listado/detalle → editar → repetir DELETE dos veces → consultar activo=false → edición devuelve 409. Como dueño/cliente las lecturas devuelven 200 y escrituras 403. Sin token, 401. Validación 400, ID inexistente 404 y conflicto 409 usan ProblemDetails.

Los cambios y CREATE/UPDATE/DELETE se confirman juntos. Los errores de PostgreSQL no exponen SQL; 40001/40P01 reintentan la transacción completa hasta tres intentos.
