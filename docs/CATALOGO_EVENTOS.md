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

## F4-05: gestión de eventos

`/api/v1/eventos`: POST, GET paginado, GET `/{id}`, PUT `/{id}`, DELETE `/{id}`. Solo dueño del recinto o administrador; el listado privado incluye todos los estados y el dueño solo ve los suyos. Cliente → 403; sin sesión → 401. POST fija BORRADOR y creadoPor desde la sesión; no se recibe creador ni estado en el cuerpo. PUT configura BORRADOR/PROGRAMADO sin historial de compras, sin cambiar recinto ni creador.

```json
{"recintoId":1,"categoriaId":1,"nombre":"Festival","fechaInicio":"2027-06-01T18:00:00Z","fechaFin":"2027-06-01T23:00:00Z","fechaInicioVenta":"2026-10-01T00:00:00Z","fechaFinVenta":"2027-06-01T17:00:00Z","maxBoletosPorCompra":5}
```

Usar IDs existentes y fechas UTC (Z). Nombre hasta 150, URL hasta 255, límite positivo; fechaFin ≥ fechaInicio, fechaFinVenta ≥ fechaInicioVenta si ambas se informan. Fechas opcionales pueden ser null; abrir venta exige ambas fechas y ventana vigente `[inicio, fin)`, además de cupo disponible en tipos activos de localidades activas o generales.

`PUT /api/v1/eventos/{id}/estado` recibe `{"estadoEsperado":"BORRADOR","estadoNuevo":"PROGRAMADO"}`. Transiciones: BORRADOR → PROGRAMADO/CANCELADO; PROGRAMADO → EN_VENTA/CANCELADO; EN_VENTA → AGOTADO/FINALIZADO/CANCELADO; AGOTADO → EN_VENTA/FINALIZADO/CANCELADO. FINALIZADO/CANCELADO son terminales. Estado esperado distinto → 409; transición inválida → 422. AGOTADO exige cupo disponible cero; FINALIZADO exige haber alcanzado fechaFin o fechaInicio si no tiene fin.

DELETE conserva el evento como CANCELADO; repetido devuelve 200 sin nueva auditoría. Con cualquier historial de compras, la edición y la cancelación devuelven 409: el flujo con devolución y reembolso se implementa en ventas/finanzas. No se crea una cancelación financiera parcial desde el CRUD. Recinto inactivo impide configurar o publicar, pero permite consultar y cancelar un evento sin compras.

Prueba local: crear recinto propio → crear evento → editar → programar → abrir venta sin tipos (422) → crear tipos en F4-07 → abrir venta dentro de la ventana → intentar editar (409). Como otro dueño, detalle/cancelación → 403 y listado sin eventos ajenos. Las transiciones se escriben con estado esperado y se auditan en la misma transacción.
