# Recintos, localidades y mapa — Fase 4

## F4-01: recintos

Preparar la API según [Desarrollo local](DESARROLLO_LOCAL.md) y autenticarse según [Swagger para frontend](FRONTEND_SWAGGER_AUTENTICACION.md). Un ADMINISTRADOR debe haber completado el cambio inicial de contraseña. Crear un dueño con `POST /api/v1/usuarios/duenos-recinto`; luego hacer login con esa cuenta y actualizar Authorize.

| Método | Ruta | Resultado / permiso |
|---|---|---|
| POST | `/api/v1/recintos` | 200 con recinto creado; dueño propio o administrador |
| GET | `/api/v1/recintos?page=1&pageSize=20` | 200 con items/page/pageSize/total; dueño ve solo suyos, administrador todos |
| GET | `/api/v1/recintos/{id}` | 200; dueño propio o administrador |
| PUT | `/api/v1/recintos/{id}` | 200 con edición completa; no cambia dueño |
| DELETE | `/api/v1/recintos/{id}` | 200; baja lógica condicional activo=true → false |

Los listados privados incluyen recintos inactivos para consultar el historial. Page admite 1–100000 y pageSize 1–100. No hay reactivación ni transferencia de dueño en este bloque. DELETE repetido devuelve éxito sin duplicar auditoría; editar un recinto inactivo devuelve 409.

Como DUENO_RECINTO, omitir `duenoId` o enviarlo null al crear: se obtiene del perfil autenticado. Un ADMINISTRADOR debe indicar el ID del perfil `duenos_recinto` de un dueño activo, no el ID de `usuarios`. El `duenoId` figura en las respuestas de recintos; no se añade un endpoint de búsqueda de dueños en este bloque.

Ejemplo de creación como dueño (todos los valores son de prueba y no contienen credenciales):

```json
{
  "duenoId": null,
  "nombre": "Recinto de prueba",
  "direccion": "Avenida Principal",
  "ciudad": "Guatemala",
  "latitud": 14.6349,
  "longitud": -90.5069,
  "imagenPlanoUrl": null,
  "capacidadTotal": 500
}
```

Para PUT usar el mismo cuerpo quitando `duenoId`. Nombre obligatorio hasta 150 caracteres; dirección 255, ciudad 100 y URL de plano 255. Capacidad positiva cuando se informa. Latitud -90..90 y longitud -180..180, hasta seis decimales. No se suben archivos en F4-01; la subida corresponde a F4-09.

La capacidad puede ser null únicamente mientras no haya localidades. Para editarla, debe cubrir la suma de todas las localidades, incluidas las inactivas. Se bloquea el recinto en la transacción; los triggers existentes V9 también protegen las capacidades. SQLSTATE 40001/40P01 reintenta toda la transacción hasta tres intentos; si persiste, devuelve 409. Las restricciones de integridad devuelven 422 sin exponer SQL.

CREATE/UPDATE/DELETE se auditan en la misma transacción del cambio. No se borran recintos, localidades ni historial. Un CLIENTE recibe 403; un dueño ajeno recibe 403; sin token, 401; ID inexistente, 404; campos inválidos, 400.

### Prueba manual de recintos

1. Como dueño, crear un recinto con el ejemplo de Swagger y guardar el `id` de la respuesta.
2. Consultar su detalle y el listado: 200; el listado muestra solo tus recintos.
3. Cambiar nombre/capacidad con PUT: 200 y datos actualizados.
4. Como otro dueño, consultar/editar/dar de baja ese ID: 403.
5. Como administrador, consultar/editar ese mismo recinto: 200.
6. Volver al dueño original, dar de baja dos veces: 200 en ambas; el detalle muestra activo=false.
7. Intentar editarlo tras la baja: 409; consultar el historial sigue permitido al dueño y administrador.

Las pruebas automatizadas comprueban JWT real, permisos en handlers, filtrado, validación, bajas idempotentes, auditoría y ejemplos Bearer de Swagger con PostgreSQL/Flyway temporales. Ejecutar con Docker operativo:

```powershell
dotnet test --project tests/Promotora.Api.Tests
```
