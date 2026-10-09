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

Flyway usa `postgres` exclusivamente para DDL y extensión pgvector. La API usa el login restringido `promotora_runtime`, miembro de `promotora_app`; no conectar la aplicación con el superusuario. Las migraciones crean los roles de permisos NOLOGIN.

Tras migrar, crear el login local (la contraseña solo se usa si el login es nuevo):

```powershell
$env:PROMOTORA_DB_PASSWORD = Read-Host 'Contraseña del login de la API' -MaskInput
Get-Content database/local/provisionar-login.sql -Raw | docker compose exec -T -e PROMOTORA_DB_PASSWORD postgres psql -U postgres -d promotora -v ON_ERROR_STOP=1
```

El archivo configura un login, no cambia tablas ni migraciones. `promotora_runtime` no es superusuario ni propietario y hereda los permisos restringidos de V9. Guardar posteriormente la conexión `Host=localhost;Port=5432;Database=promotora;Username=promotora_runtime;Password=...` en user-secrets, no en el repositorio. Si la contraseña contiene `;` o comillas, usar el escapado de cadenas de conexión Npgsql.

F2-03 mapea 26 tablas (198 columnas) y 16 vistas `bi` sin clave, con snake_case, fechas UTC y `vector(1536)`. Las clases `*Row` son modelos de Infrastructure para persistencia; los adaptadores mapearán manualmente a entidades del dominio con reglas en sus fases correspondientes. No son contratos de la API. Flyway sigue siendo la única herramienta que crea el esquema; no ejecutar `EnsureCreated`, `EnsureDeleted` ni migraciones EF.

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests
```

Esta prueba crea PostgreSQL temporal con Testcontainers, aplica las migraciones reales mediante Flyway y consulta con un login restringido. Docker debe estar encendido; no requiere Compose ni modifica la base local. Comprueba todas las columnas/vistas, defaults e IDs, perfil obligatorio, permisos, JSON, clave compuesta y persistencia/búsqueda por coseno de vectores.

## Solución (F2-01)

```powershell
dotnet restore
dotnet build -warnaserror
dotnet format --verify-no-changes
```

Los cinco proyectos de pruebas usan xUnit v3 y Microsoft.Testing.Platform. En F2-01 están vacíos; las tareas siguientes añaden las pruebas reales antes de cerrar la fase. No hay endpoints de negocio todavía.
