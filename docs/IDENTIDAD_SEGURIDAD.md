# Identidad y seguridad — Fase 3

## F3-01: altas y bootstrap

`POST /api/v1/auth/registro` crea CLIENTE. `POST /api/v1/usuarios/administradores` y
`POST /api/v1/usuarios/duenos-recinto` requieren que el actor sea ADMINISTRADOR activo
y haya cambiado su contraseña inicial; se comprueba también dentro del handler contra PostgreSQL.
Las altas normales crean administradores ESTANDAR; el bootstrap es el único mecanismo inicial de SUPERADMIN.

Ejemplo de registro (escribe tu contraseña local en Swagger; no guardes cuerpos con contraseñas):

```json
{"email":"cliente@example.test","password":"<contraseña elegida localmente>","nombre":"Cliente","apellido":"Prueba","telefono":null,"fechaNacimiento":null}
```

Las contraseñas tienen entre 12 y 128 caracteres. El correo se normaliza a minúsculas.
Se usa PasswordHasher de Microsoft Identity v3, PBKDF2-HMAC-SHA512 con 210.000 iteraciones y sal aleatoria.
Referencia: [código oficial](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/PasswordHasher.cs)
y [recomendaciones OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
No se almacenan contraseñas en claro. Usuario, perfil, auditoría y outbox se confirman juntos.
La restricción de perfil diferida se valida al COMMIT. Un correo duplicado produce 409; validación, 400.
El outbox `usuario.registrado` contiene usuarioId/email; su consumidor de bienvenida se implementará en F6.

El bootstrap utiliza un advisory lock de transacción para crear un único SUPERADMIN entre instancias concurrentes.
Si ya existe uno, conserva sus datos y contraseña. No crea cuentas financieras (corresponde a F7).
Sin parámetros de bootstrap se omite la inicialización; si solo se proporciona uno, el arranque falla con mensaje genérico.

Para preparar el usuario inicial en PowerShell 7, con la base local preparada según DESARROLLO_LOCAL.md:

```powershell
dotnet user-secrets set "Bootstrap:AdminEmail" "admin@example.test" --project src/Promotora.WebApi
$bootstrapPassword = Read-Host "Contraseña del administrador (12–128 caracteres)" -MaskInput
dotnet user-secrets set "Bootstrap:AdminPassword" $bootstrapPassword --project src/Promotora.WebApi
$bootstrapPassword = $null
$jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project src/Promotora.WebApi
$jwtKey = $null
dotnet run --project src/Promotora.WebApi
```

Usuario de prueba: **admin@example.test**. Contraseña: la que ingresaste en el comando anterior.
Los valores se guardan fuera del repositorio mediante user-secrets, exclusivamente para desarrollo.
El administrador inicial tendrá `debeCambiarPassword=true`; debe usar login y cambio obligatorio antes de gestionar usuarios.
Las altas privilegiadas rechazan actores anónimos; no hay mecanismo para enviar un ID de usuario mediante headers.
Swagger: http://localhost:5080/swagger. Las rutas se aceptan sin distinguir mayúsculas/minúsculas.

## F3-02: sesiones

`POST /api/v1/auth/login` recibe email/password y devuelve accessToken, refreshToken,
expiraEn, debeCambiarPassword y usuarioId. `POST /api/v1/auth/refresh` recibe refreshToken;
`POST /api/v1/auth/logout` recibe refreshToken y requiere Bearer del mismo usuario.
JWT HS256: firma, emisor, audiencia, expiración y algoritmo se validan. La clave externa
`Jwt:SigningKey` es Base64 de 32 bytes aleatorios o más. Sin clave válida y conexión configurada
el arranque falla. Defaults: issuer promotora, audience promotora-api, accessMinutes 15 y refreshDays 7;
las duraciones son configurables entre 1–30 minutos/días respectivamente.

Cada refresh rota: el anterior queda revocado y apunta al sucesor. Al reutilizarlo se revoca
toda la familia y se registra LOGOUT sobre refresh_tokens (catálogo de acciones existente).
El bloqueo de usuarios serializa login/refresh/logout con las futuras bajas y cambios de contraseña.
Los rechazos esperados de sesión confirman revocaciones/auditoría; las excepciones revierten.
Solo se guarda SHA-256 de 32 bytes de entropía del refresh, nunca su valor en claro.
El JWT incluye el ID de sesión; cada petición comprueba su vigencia y el estado/rol actual del usuario.
Por tanto, rotar, reutilizar o cerrar una sesión invalida también sus access tokens anteriores.

Un usuario con cambio obligatorio recibe JWT de 5 minutos y refreshToken=null.
Las policies de F3-03 limitarán su uso al cambio de contraseña propio de F3-04.
Tras cambiarla deberá hacer login de nuevo. Login fallido usa 401 genérico, sin indicar si el correo existe;
la verificación de usuarios inexistentes realiza un hash de coste equivalente.

## F3-03: autorización

Policies Cliente/Administrador/DuenoRecinto exigen rol exacto, autenticación y contraseña inicial cambiada.
SesionCompleta exige autenticación sin cambio pendiente. CambioPassword admite la sesión limitada,
pero su handler comprobará que el ID solicitado sea el propio (ningún admin cambia la contraseña ajena).
Cliente y dueño solo gestionan su usuario/perfil; administrador habilitado puede gestionar otros usuarios.
El rol y estado actuales se vuelven a consultar en Application: una claim por sí sola no autoriza la propiedad.
Las altas de perfiles privilegiados usan policy Administrador y comprobación adicional en el handler.
Un ADMINISTRADOR no recibe permisos de CLIENTE/DUENO automáticamente; no hay herencia implícita de roles.

## F3-04: usuarios/perfiles y contraseña

| Ruta | Permiso | Comportamiento |
|---|---|---|
| GET /api/v1/usuarios?page=1&pageSize=20 | Administrador | Lista ordenada por ID; page 1–100.000, pageSize 1–100, items/total |
| GET /api/v1/usuarios/{id} | Propietario o administrador | Usuario y perfil, nunca hashes ni tokens |
| PUT /api/v1/usuarios/{id} | Propietario o administrador | Reemplaza email/nombre/apellido y campos del perfil correspondiente |
| DELETE /api/v1/usuarios/{id} | Propietario o administrador | UPDATE condicional a INACTIVO y revocación de sesiones; conserva perfil/historial |
| PUT /api/v1/usuarios/{id}/password | Solo propietario, incluida sesión limitada | passwordActual/passwordNuevo; revoca sesiones y quita cambio obligatorio |

El cuerpo de edición contiene email/nombre/apellido y, para cliente, fechaNacimiento/telefono;
para dueño, empresa/nit. Un dueño requiere empresa. No acepta cambios de rol ni nivel de acceso.
Los campos de otro perfil no se aplican. Cambiar el correo revoca sesiones; después se inicia sesión con el correo nuevo.
La baja repetida por un administrador devuelve éxito sin otra auditoría. El usuario dado de baja pierde acceso inmediatamente.
Los locks sobre actor y destinatario se toman por ID ascendente para evitar inversiones de orden.
La alta privilegiada vuelve a comprobar al administrador bajo lock, evitando carreras con su baja.

Para el primer inicio: login con admin@example.test y tu contraseña inicial → copiar accessToken
en **Authorize** de Swagger (solo el token, sin escribir Bearer) → PUT /usuarios/{usuarioId}/password
con passwordActual y passwordNuevo → nuevo login con passwordNuevo → actualizar Authorize.
La sesión limitada no permite listar/consultar perfiles ni crear otros administradores.
Los errores usan ProblemDetails: 400 validación, 401 sesión/contraseña, 403 propiedad/rol,
404 usuario ausente, 409 correo/estado en conflicto y 422 regla de contraseña/empresa.

## F3-05: límites y CORS

Login: 5 solicitudes/minuto/IP. Registro: 10/minuto/IP. Ventanas y contadores independientes,
sin cola de espera. Configuración: RateLimiting:LoginPermitLimit y RateLimiting:RegistroPermitLimit (1–1000).
El límite devuelve 429 application/problem+json con traceId y Retry-After en segundos.
Se usa la IP de la conexión, no headers enviados por el cliente. El contador es local a cada instancia;
una futura publicación con múltiples réplicas deberá aplicar también un límite compartido en el proxy.

Cors:AllowedOrigins es una lista explícita, por ejemplo variable de entorno
`Cors__AllowedOrigins__0=https://frontend.example.test`. Sin lista no se permiten orígenes externos;
Swagger en el mismo origen sigue funcionando. No se aceptan comodines, rutas, slash final ni cookies cross-origin.
El frontend se decide en F11; aquí no se fija ninguno. CORS controla el navegador, no reemplaza autenticación.

## F3-06: auditoría

IAuditLogger añade filas solo INSERT a logs_auditoria, confirmadas por la transacción del caso de uso.
El catálogo existente se conserva: CREATE (alta/bootstrap), UPDATE (perfil/contraseña), DELETE (baja lógica),
LOGIN, LOGIN_FAILED y LOGOUT. Los motivos distinguen bootstrap, contraseña cambiada/rechazada,
cierre solicitado, refresh reutilizado y sesión expirada/inhabilitada.
detalle JSONB solo admite motivo enum y familiaId opcional; nunca recibe cuerpos HTTP ni credenciales.
usuario_id identifica al actor, entidad_id al destinatario; bootstrap y correo de login inexistente permiten actor null.
La auditoría de reutilización y fallos de login se confirma aunque el resultado HTTP sea 401.
Las lecturas de perfiles/listados no agregan acciones fuera del catálogo; se auditan las mutaciones.
Los triggers y permisos Flyway existentes siguen protegiendo UPDATE/DELETE/TRUNCATE de logs.

## F3-07: cómo verificar el módulo

Preparar la base y secretos con [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md), iniciar la API y abrir Swagger.
En el primer login usar el usuario configurado en Bootstrap:AdminEmail y la contraseña elegida fuera del repo;
seguir el cambio obligatorio descrito arriba. Crear luego un CLIENTE por registro y un DUENO por alta administrativa.
Con sus JWT: GET del usuario propio debe dar 200; GET de otro y listado administrativo, 403.
Sin JWT, listado debe dar 401. Un refresh antiguo repetido debe dar 401 e invalidar el sucesor.

Ejecutar las pruebas del API con `dotnet test --project tests/Promotora.Api.Tests` (requiere Docker):
validan bootstrap/cambio inicial, altas, roles/propiedad, errores, firma/emisor/audiencia/expiración/algoritmo,
rotación/reutilización/logout y baja. Usan PostgreSQL/Flyway y JWT reales, sin base local ni credenciales fijas.
Las pruebas Infrastructure incluyen carreras de bootstrap y refresh, atomicidad y protección de auditoría.
Evidencia y revisión completa: [VALIDACION_FASE_3.md](VALIDACION_FASE_3.md).
