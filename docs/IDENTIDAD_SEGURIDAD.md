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
dotnet run --project src/Promotora.WebApi
```

Usuario de prueba: **admin@example.test**. Contraseña: la que ingresaste en el comando anterior.
Los valores se guardan fuera del repositorio mediante user-secrets, exclusivamente para desarrollo.
El administrador inicial tendrá `debeCambiarPassword=true`; login y cambio obligatorio se incorporan en F3-02/F3-04.
Hasta F3-02 las altas privilegiadas rechazan actores anónimos; no hay mecanismo para enviar un usuario mediante headers.
Swagger: http://localhost:5080/swagger. Las rutas se aceptan sin distinguir mayúsculas/minúsculas.
