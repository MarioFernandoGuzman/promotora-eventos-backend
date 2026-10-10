# Acceso a Swagger y autenticación para frontend

Esta guía describe los contratos implementados en Fase 3. Sirve para probar la API con Swagger e integrar la sesión en el frontend. Los ejemplos contienen marcadores: sustituirlos localmente, sin guardar contraseñas ni tokens reales en este documento.

## 1. Direcciones y requisitos

| Recurso | Dirección local |
|---|---|
| API base | `http://localhost:5080/api/v1` |
| Swagger UI | `http://localhost:5080/swagger` |
| Contrato OpenAPI | `http://localhost:5080/swagger/v1/swagger.json` |
| Proceso activo | `http://localhost:5080/health/live` |
| Base preparada | `http://localhost:5080/health/ready` |

`localhost` apunta al equipo donde abres el navegador. Si el backend se ejecuta en otro equipo, el responsable debe proporcionar su URL accesible. El perfil local actual escucha en localhost; esta guía no configura despliegues remotos.

El responsable del backend debe preparar Docker, migraciones, login restringido `promotora_runtime`, conexión, clave JWT y administrador inicial siguiendo [Desarrollo local](DESARROLLO_LOCAL.md). Estas claves son configuración del servidor: no se entregan al frontend. Para probar solo se necesita una cuenta de la aplicación y su contraseña.

Swagger se habilita en `Development`. Con el entorno local ya configurado, ejecutar desde la raíz del repositorio:

```powershell
dotnet run --project src/Promotora.WebApi
```

Dejar esa terminal abierta. Esperar a `Now listening on: http://localhost:5080`. En otra terminal se puede comprobar la base:

```powershell
Invoke-RestMethod http://localhost:5080/health/ready
```

Debe responder HTTP 200. Si responde 503, resolver conexión, permisos o migraciones con el responsable del backend antes de probar login.

## 2. Cuenta de prueba

El administrador inicial usa el correo configurado en `Bootstrap:AdminEmail`; en la guía local es `admin@example.test`. Su contraseña es la elegida al configurar `Bootstrap:AdminPassword`. No existe una contraseña predeterminada versionada.

Si el SUPERADMIN ya existe, cambiar la configuración de bootstrap no cambia su contraseña. Usar la contraseña vigente de esa cuenta.

Para probar un CLIENTE, abrir `POST /api/v1/auth/registro`, pulsar **Try it out**, completar y ejecutar:

```json
{
  "email": "cliente.frontend@example.test",
  "password": "<contraseña elegida localmente>",
  "nombre": "Cliente",
  "apellido": "Frontend",
  "fechaNacimiento": null,
  "telefono": null
}
```

Las contraseñas nuevas deben tener entre 12 y 128 caracteres. El registro devuelve HTTP 200 con los datos del usuario; no inicia una sesión ni devuelve tokens. Un correo ya registrado devuelve 409. El registro público siempre crea CLIENTE: no admite elegir un rol. Los administradores y dueños se crean mediante las altas administrativas de la sección 8.

## 3. Iniciar sesión en Swagger

1. Abrir `http://localhost:5080/swagger`.
2. Expandir **Auth → POST /api/v1/auth/login**.
3. Pulsar **Try it out**.
4. Introducir correo y contraseña de la cuenta de prueba.
5. Pulsar **Execute**.
6. Comprobar HTTP 200 y copiar el valor de `accessToken`.

Cuerpo de login:

```json
{
  "email": "admin@example.test",
  "password": "<contraseña vigente de esta cuenta>"
}
```

La respuesta es un objeto directo, sin envoltorio `data` o `result`. Ejemplo ilustrativo; los tokens, el ID y la fecha se obtienen de tu respuesta real:

```json
{
  "accessToken": "<JWT recibido>",
  "refreshToken": "<refresh recibido o null si el cambio es obligatorio>",
  "expiraEn": "2030-01-01T12:15:00Z",
  "debeCambiarPassword": false,
  "usuarioId": 1
}
```

Cuando `debeCambiarPassword` es `true`, el valor real de `refreshToken` es JSON `null`, sin comillas. `expiraEn` es la expiración UTC del access token. Por defecto una sesión normal tiene access token de 15 minutos y refresh de 7 días; el servidor puede configurar estas duraciones. La sesión inicial restringida dura 5 minutos y no entrega refresh.

## 4. Autorizar Swagger

1. Pulsar **Authorize**, arriba de Swagger.
2. En el esquema **Bearer**, pegar solamente el valor de `accessToken`, sin comillas ni prefijo `Bearer`.
3. Pulsar **Authorize** dentro del diálogo.
4. Pulsar **Close**.

Swagger añade automáticamente el encabezado a los endpoints protegidos:

```http
Authorization: Bearer <accessToken>
```

El `refreshToken` no se pega en Authorize. Swagger no inicia sesión por sí mismo ni renueva los tokens automáticamente. Si cambias de usuario o renuevas la sesión, abre **Authorize**, retira la autorización anterior con **Logout** y pega el nuevo access token. El botón **Logout** de ese diálogo solo elimina el token de Swagger: para revocar la sesión en el servidor se usa el endpoint de la sección 7.

## 5. Cambio obligatorio de contraseña

Si el login devuelve `debeCambiarPassword=true`, seguir este flujo antes de probar otras operaciones:

1. Autorizar Swagger con ese access token inicial.
2. Abrir `PUT /api/v1/usuarios/{id}/password` y pulsar **Try it out**.
3. En `id`, colocar el `usuarioId` devuelto por login. No asumir que siempre es 1.
4. Completar el cuerpo con la contraseña vigente y otra diferente, de 12 a 128 caracteres.

```json
{
  "passwordActual": "<contraseña inicial vigente>",
  "passwordNuevo": "<nueva contraseña elegida localmente>"
}
```

5. Ejecutar y comprobar HTTP 200.
6. Volver a `POST /api/v1/auth/login` con el mismo correo y la nueva contraseña.
7. Comprobar `debeCambiarPassword=false` y un refresh token no nulo.
8. Sustituir el token anterior en **Authorize** por el nuevo `accessToken`.

El cambio revoca todas las sesiones anteriores del usuario, incluido el token que acabas de usar. El token inicial no permite listar usuarios ni consultar perfiles: esas llamadas devolverán 403. Solo el propietario puede cambiar su contraseña; un administrador no puede usar este endpoint para cambiar la de otra persona.

## 6. Comprobar que la autorización funciona

Con una sesión completa, ejecutar `GET /api/v1/usuarios/{id}` usando tu `usuarioId`. Debe responder 200 con `usuario` y `perfil`.

Con un ADMINISTRADOR, probar también:

```http
GET /api/v1/usuarios?page=1&pageSize=20
Authorization: Bearer <accessToken>
```

La respuesta contiene `items`, `page`, `pageSize` y `total`. `page` admite 1–100000 y `pageSize` 1–100.

Con CLIENTE o DUENO_RECINTO, consultar el propio usuario devuelve 200; consultar el usuario ajeno o el listado administrativo devuelve 403. Sin autorización, el listado devuelve 401. Un candado autorizado en Swagger indica que hay un token configurado; la API sigue comprobando su vigencia, rol y propiedad en cada petición.

## 7. Renovación y cierre de sesión

### Renovar

Abrir `POST /api/v1/auth/refresh`. No requiere encabezado Authorization; la credencial es el refresh token vigente en el cuerpo:

```json
{
  "refreshToken": "<último refreshToken recibido>"
}
```

Con HTTP 200, sustituir **ambos** tokens por los de esta nueva respuesta y actualizar **Authorize**. La rotación invalida el refresh anterior y también el access token asociado a él, aunque su fecha de expiración todavía no haya llegado.

Reutilizar un refresh revocado devuelve 401 y revoca toda su familia, incluido su sucesor. Para el frontend, centralizar la renovación: varias peticiones que necesiten renovar deben compartir una única operación en curso. No enviar renovaciones simultáneas con el mismo token ni reintentar ciegamente una renovación cuyo resultado se desconoce. Si no se dispone del token sucesor, volver a login.

Cuando el refresh devuelve 401, limpiar la sesión local y pedir login. No intentar renovar una sesión inicial con `refreshToken=null`.

### Cerrar

Abrir `POST /api/v1/auth/logout` usando **Authorize** con el access token vigente y enviar el refresh token de esa misma sesión:

```json
{
  "refreshToken": "<refreshToken vigente de esta sesión>"
}
```

Con HTTP 200, eliminar la sesión del frontend y retirar el token de Swagger. El endpoint revoca la familia de la sesión; el access token deja de servir. Si la sesión ya no es válida, puede responder 401 y se debe limpiar igualmente la sesión local.

## 8. Operaciones disponibles y permisos

Todas las rutas de esta tabla comienzan con `/api/v1`. Las operaciones exitosas actuales responden HTTP 200.

| Método | Ruta | Acceso |
|---|---|---|
| POST | `/auth/registro` | Público; crea CLIENTE |
| POST | `/auth/login` | Público; recibe email/password |
| POST | `/auth/refresh` | Público; requiere refresh vigente en el cuerpo |
| POST | `/auth/logout` | Sesión completa; tokens de la misma sesión |
| POST | `/usuarios/administradores` | ADMINISTRADOR con sesión completa; crea ESTANDAR |
| POST | `/usuarios/duenos-recinto` | ADMINISTRADOR con sesión completa |
| GET | `/usuarios` | ADMINISTRADOR con sesión completa; paginado |
| GET | `/usuarios/{id}` | Propietario o ADMINISTRADOR con sesión completa |
| PUT | `/usuarios/{id}` | Propietario o ADMINISTRADOR con sesión completa |
| DELETE | `/usuarios/{id}` | Propietario o ADMINISTRADOR con sesión completa; baja lógica |
| PUT | `/usuarios/{id}/password` | Solo propietario; admite sesión inicial restringida |

Consultar los cuerpos de edición y altas en **Schemas** de Swagger. La edición no cambia rol ni nivel de acceso. Cambiar el correo revoca las sesiones y exige login con el correo nuevo; la baja desactiva el usuario e impide autenticarse. Los módulos de recintos, catálogo, compras y finanzas aún no tienen endpoints de negocio implementados en esta fase.

## 9. Consumir la API desde el frontend

Configurar la URL base `http://localhost:5080/api/v1` en el entorno del frontend. Para cuerpos JSON enviar `Content-Type: application/json`; para las rutas protegidas, `Authorization: Bearer <accessToken>`. El contrato actual devuelve tokens en JSON y no usa cookies de autenticación ni requiere `credentials: include`.

El frontend debe dirigir a cambio de contraseña cuando `debeCambiarPassword=true`, conservar el `usuarioId` recibido, gestionar `expiraEn` en UTC y reemplazar los dos tokens al renovar. Para conocer los datos del usuario autenticado, usar `GET /usuarios/{usuarioId}` después del cambio obligatorio. No existe un endpoint `/auth/me` en esta implementación. El backend decide los permisos; ocultar botones en la interfaz no sustituye esa validación.

No incluir claves JWT, credenciales de base de datos, contraseñas ni tokens reales en el código frontend, archivos versionados, ejemplos compartidos o logs. La estrategia de almacenamiento de sesión del frontend se decidirá con su arquitectura en Fase 11; el contrato actual no añade un intermediario ni cookies HttpOnly.

### CORS local

Swagger funciona en el origen de la propia API. Un frontend abierto en otro puerto necesita que el backend permita su origen exacto. Por ejemplo, si tu frontend realmente usa `http://localhost:5173`, configurar en la terminal de la API antes de arrancarla:

```powershell
$env:Cors__AllowedOrigins__0 = 'http://localhost:5173'
```

Después arrancar o reiniciar la API:

```powershell
dotnet run --project src/Promotora.WebApi
```

Sustituir el ejemplo por el origen real, sin ruta ni barra final. `http://127.0.0.1:5173` es otro origen distinto de `http://localhost:5173`. Sin lista configurada no se permiten orígenes externos. No se permiten comodines ni cookies entre orígenes.

El servidor envía `Retry-After` y `X-Correlation-ID`, pero la política CORS actual no los expone para su lectura desde JavaScript en otro origen. Usar `traceId` del cuerpo de error para reportar fallos; no asumir que el frontend puede leer esos encabezados. Swagger en el mismo origen sí puede mostrarlos.

## 10. Errores y resolución

Los errores HTTP usan `application/problem+json`, con `status`, `title` y `traceId`; `detail`, `codes` y `errors` pueden aparecer según el origen del error. El frontend debe tolerar su ausencia. Las validaciones pueden incluir `errors` como objeto cuyas propiedades contienen listas de mensajes. Reportar ruta, código HTTP y `traceId`, sin copiar credenciales.

| Resultado | Causa / siguiente paso |
|---|---|
| 400 | Revisar campos, formato de correo y longitud de contraseña con `errors` |
| 401 en login | Credenciales incorrectas o usuario inactivo; respuesta genérica |
| 401 en ruta protegida | Token ausente, inválido, expirado o sesión revocada; revisar la sesión |
| 401 en refresh | Refresh inválido, expirado o reutilizado; volver a login |
| 403 | Rol/propiedad insuficientes o cambio de contraseña pendiente; no resolver con renovaciones repetidas |
| 404 | Recurso inexistente o ruta incorrecta |
| 409 | Conflicto, por ejemplo correo ya registrado |
| 422 | Regla de negocio, por ejemplo contraseña nueva igual a la anterior |
| 429 | Límite por IP: login 5/minuto, registro 10/minuto por defecto; esperar antes de reintentar |
| 500 | Error inesperado; comunicar `traceId` al backend |
| Swagger no abre | Comprobar proceso, puerto 5080 y entorno Development |
| Bootstrap impide arrancar | Completar conexión, correo y contraseña externos según Desarrollo local |
| Login funciona en Swagger pero el navegador bloquea el frontend | Revisar el origen permitido de CORS y la pestaña Network |

No interpretar un fallo CORS como contraseña incorrecta. Tampoco lanzar un ciclo infinito de login o refresh ante cualquier error HTTP.

## 11. Lista de comprobación de la integración

- [ ] Swagger abre y `/health/ready` devuelve 200.
- [ ] Login devuelve 200 y se utiliza `accessToken` en Authorize.
- [ ] El administrador inicial completa cambio obligatorio y nuevo login.
- [ ] La consulta del usuario propio devuelve 200.
- [ ] Un CLIENTE recibe 403 al intentar listar usuarios y 401 sin token.
- [ ] Refresh sustituye ambos tokens; se descarta el access token anterior.
- [ ] Logout revoca la sesión y limpia el estado de la interfaz.
- [ ] Las solicitudes desde el origen real del frontend pasan CORS.
- [ ] Se muestran errores de validación, permisos y límite sin registrar secretos.

Para verificar los contratos con las pruebas automatizadas del backend, con Docker operativo:

```powershell
dotnet test --project tests/Promotora.Api.Tests
```

Las pruebas usan PostgreSQL temporal; no necesitan credenciales del administrador local. Detalles de implementación y evidencia: [Identidad y seguridad](IDENTIDAD_SEGURIDAD.md) y [Validación de Fase 3](VALIDACION_FASE_3.md).
