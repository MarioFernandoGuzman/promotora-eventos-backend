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

## F4-02: localidades

Las rutas privadas de zonas se anidan bajo el recinto. Todas requieren Bearer de sesión completa y dueño propio o administrador:

| Método | Ruta | Resultado |
|---|---|---|
| POST | `/api/v1/recintos/{recintoId}/localidades` | 200 con localidad creada |
| GET | `/api/v1/recintos/{recintoId}/localidades?page=1&pageSize=20` | 200 con items/page/pageSize/total |
| GET | `/api/v1/recintos/{recintoId}/localidades/{id}` | 200 con detalle |
| PUT | `/api/v1/recintos/{recintoId}/localidades/{id}` | 200; reemplaza nombre/capacidad/geometría/color |
| DELETE | `/api/v1/recintos/{recintoId}/localidades/{id}` | 200; baja lógica idempotente |

Ejemplo de POST y PUT en Swagger:

```json
{
  "nombre": "Palco",
  "capacidad": 100,
  "geometria": {"type": "polygon", "points": [[10,20],[80,20],[80,60],[10,60]]},
  "color": "#3366CC"
}
```

Nombre obligatorio hasta 100 caracteres y capacidad positiva. Geometría y color pueden ser null. El formato del polígono exige al menos tres puntos distintos, cada uno [x,y] con números finitos; usa coordenadas del plano, no implica normalización 0..1. No se verifica intersección de aristas ni encaje en la imagen porque la BD no almacena sus dimensiones. El color se normaliza a mayúsculas y admite exactamente #RRGGBB.

La capacidad total del recinto debe estar configurada antes de crear localidades. Las localidades inactivas siguen contando para la suma y sus nombres siguen reservados por el índice único. No se cambia recinto ni se reactivan localidades al editar. POST/PUT rechazan un recinto inactivo; consulta privada y baja de localidades conservan acceso al historial de ese recinto.

Reducir capacidad por debajo del cupo total asignado a esa localidad en cualquiera de sus eventos devuelve 422, contando también tipos de entrada inactivos. Los triggers V9 siguen siendo la protección final de integridad. No se modifica cupo_disponible ni se reserva inventario en este bloque.

Prueba manual: crear Palco 60 en un recinto de capacidad 100; intentar crear General 41 → 422; crear General 40 → 200. Un nombre repetido → 409. Polígono de un punto o color "rojo" → 400. Dar de baja Palco no libera su capacidad física: intentar crear otra zona 1 → 422. Como otro dueño, POST/GET/PUT/DELETE → 403. No usar un ID de localidad bajo otro recinto: 404 para el gestor de ese recinto.

La prueba de concurrencia usa un dueño y un administrador, con dos peticiones simultáneas para zonas de 60 sobre un recinto de 100: una devuelve 200 y otra 422; persiste solo una zona y una auditoría de CREATE. Las pruebas también comprueban conservación del historial, nombres únicos, JSONB, color y protección de cupos por evento.

## F4-03: mapa público

`GET /api/v1/recintos/{id}/mapa` no requiere JWT. Muestra un recinto activo, su nombre, imagenPlanoUrl y todas sus localidades activas con capacidad, geometría y color. Es una configuración compuesta del mapa completo, no el listado paginado privado de localidades. No incluye dueño, usuarios, compras ni finanzas.

Sin `eventoId`, eventoId/estadoEvento/cupoDisponible son null y los tipos de entrada son listas vacías. La capacidad física no es inventario de boletos.

Para disponibilidad y precios usar `GET /api/v1/recintos/{id}/mapa?eventoId={eventoId}`. El evento debe pertenecer al recinto y estar PROGRAMADO, EN_VENTA o AGOTADO. Recinto inactivo, evento ajeno, BORRADOR/CANCELADO/FINALIZADO o recurso inexistente devuelve 404. Un eventoId no positivo devuelve 400.

Ejemplo ilustrativo de respuesta con evento; IDs y cupos dependen de tus datos reales:

```json
{
  "id": 1,
  "nombre": "Recinto de prueba",
  "imagenPlanoUrl": "/planos/prueba.png",
  "eventoId": 2,
  "estadoEvento": "EN_VENTA",
  "localidades": [
    {
      "id": 3,
      "nombre": "Palco",
      "capacidad": 100,
      "geometria": {"type":"polygon","points":[[10,20],[80,20],[80,60],[10,60]]},
      "color": "#3366CC",
      "cupoDisponible": 12,
      "tiposEntrada": [{"id":4,"nombre":"Palco","precio":150.50,"cupoTotal":40,"cupoDisponible":12}]
    }
  ],
  "entradasGenerales": [{"id":5,"nombre":"General","precio":90.00,"cupoTotal":10,"cupoDisponible":7}]
}
```

Cada localidad suma cupo_disponible de sus tipos activos para ese evento; una zona sin tipos activos tiene 0. Tipos de localidades inactivas no se publican. Los tipos sin localidad se devuelven aparte en entradasGenerales. Geometría/color/imagen pueden ser null: el frontend debe ofrecer una representación alternativa mientras se configura el plano. Los precios son decimales del contrato; no inferir un único precio por zona cuando tiene varios tipos de entrada.

Las consultas muestran el inventario leído; no reservan ni garantizan que siga disponible después. PROGRAMADO o AGOTADO no implican que se pueda comprar. Las validaciones de ventana de venta y reserva atómica corresponden a F5. En F4-01–03 aún no se crean eventos/tipos por HTTP ni se suben imágenes; la prueba de inventario usa datos temporales de Testcontainers. No añadir datos manualmente a las migraciones para probarlo.

### Prueba manual del mapa

1. Crear recinto y localidades con un dueño siguiendo los pasos anteriores.
2. Retirar la autorización de Swagger y ejecutar GET del mapa sin eventoId: 200, zonas activas y cupoDisponible=null.
3. Verificar nombre, geometría y color contra la respuesta del CRUD.
4. Dar de baja una zona con el dueño; consultar el mapa sin JWT y comprobar que desaparece.
5. Dar de baja el recinto; consultar su mapa público y comprobar 404.
6. Cuando exista un evento PROGRAMADO/EN_VENTA/AGOTADO con tipos de entrada, enviar su eventoId y comprobar sus precios/cupos. Hasta F4-05/F4-07 esta parte se verifica con las pruebas automatizadas, sin endpoint de alta de evento en este bloque.

Las pruebas verifican acceso anónimo, separación de cupo/capacidad, entradas generales, tipos/localidades inactivos, borradores, evento ajeno, recinto inactivo y conservación de cupo tras consultas repetidas. En Swagger el mapa no tiene requisito Bearer; el CRUD de recintos/localidades sí lo tiene.
