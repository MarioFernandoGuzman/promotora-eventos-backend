# Desarrollo local

Requisitos: .NET 10 SDK, Docker Desktop con contenedores Linux, Docker Compose y PowerShell 7. Ejecutar desde la raíz del repositorio.

## Infraestructura (F2-02)

Leer las contraseñas sin guardarlas en archivos ni escribirlas literalmente en el historial:

```powershell
$env:POSTGRES_PASSWORD = Read-Host 'Contraseña local de PostgreSQL' -MaskInput
$env:RABBITMQ_PASSWORD = Read-Host 'Contraseña local de RabbitMQ' -MaskInput
docker compose config --quiet
docker compose up -d --wait
docker compose run --rm flyway migrate
docker compose run --rm flyway validate
docker compose run --rm flyway info
docker compose ps
```

Postgres: `localhost:5432`, base `promotora`. RabbitMQ: `localhost:5672`, usuario `promotora`, [management](http://localhost:15672). MailHog: SMTP `localhost:1025`, [bandeja](http://localhost:8025). MailHog captura mensajes; no envía correo real. No se crea ningún usuario de la aplicación en las migraciones.

Los puertos están vinculados a localhost. Los volúmenes conservan los datos. Para detener servicios, `docker compose stop`; para iniciarlos de nuevo, `docker compose up -d --wait`. No eliminar volúmenes. Las contraseñas inicializan servicios nuevos; cambiar variables no cambia las contraseñas de datos ya inicializados.

Flyway usa `postgres` exclusivamente para DDL y extensión pgvector. La API debe usar un login restringido miembro de `promotora_app`; no conectar la aplicación con el superusuario. Las migraciones crean los roles de permisos NOLOGIN. La configuración de ese login se documentará junto con la conexión EF en F2-03.

## Solución (F2-01)

```powershell
dotnet restore
dotnet build -warnaserror
dotnet format --verify-no-changes
```

Los cinco proyectos de pruebas usan xUnit v3 y Microsoft.Testing.Platform. En F2-01 están vacíos; las tareas siguientes añaden las pruebas reales antes de cerrar la fase. No hay endpoints de negocio todavía.
