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

## API y Swagger (F2-05)

Después de crear `promotora_runtime`, configurar la conexión en la misma terminal (el builder escapa la contraseña correctamente):

```powershell
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Host'] = 'localhost'
$connection['Port'] = 5432
$connection['Database'] = 'promotora'
$connection['Username'] = 'promotora_runtime'
$connection['Password'] = $env:PROMOTORA_DB_PASSWORD
$env:ConnectionStrings__Promotora = $connection.ConnectionString
dotnet run --project src/Promotora.WebApi
```

Alternativa: guardar `ConnectionStrings:Promotora` en user-secrets de WebApi y Worker con `dotnet user-secrets set 'ConnectionStrings:Promotora' (Read-Host 'Cadena de conexión restringida' -MaskInput) --project src/Promotora.WebApi` y repetir cambiando el proyecto a `src/Promotora.Worker`. Los secretos quedan fuera del repositorio. No imprimir la conexión ni usar `user-secrets list` para compartir capturas. No se requiere OpenAI en Fase 2.

Abrir [Swagger UI](http://localhost:5080/swagger). Por ahora el documento tiene `paths: {}`: no hay CRUD, login ni ventas implementados. Los health checks son técnicos y están fuera del catálogo Swagger. Los futuros controllers heredan `ApiControllerBase`, cuya ruta es `/api/v1/[controller]`; la serialización usa camelCase. Los `Result` fallidos se convierten en 400/401/403/404/409/422/429 con ProblemDetails. Excepciones inesperadas devuelven 500 genérico; el log registra tipo de excepción y correlación, sin mensajes privados ni payloads.

Desde otra terminal:

```powershell
Invoke-WebRequest http://localhost:5080/health/live
Invoke-WebRequest http://localhost:5080/health/ready
Invoke-RestMethod http://localhost:5080/swagger/v1/swagger.json
dotnet test --project tests/Promotora.Api.Tests
```

`live` responde 200 si el proceso está activo. `ready` responde 200 solo con conexión, login restringido miembro de `promotora_app`, 26 tablas y 16 vistas; sin conexión, migraciones o permisos adecuados devuelve 503 sin detalles SQL. Swagger se habilita únicamente en Development (perfil local incluido). Las respuestas llevan `X-Correlation-ID`; se conserva un UUID válido enviado por el cliente y se genera otro si falta o es inválido. El mismo identificador aparece como `traceId` en ProblemDetails y en Serilog.

El Worker se inicia con `dotnet run --project src/Promotora.Worker` en otra terminal con la misma variable de conexión, o sus propios user-secrets. En Fase 2 es un host preparado con DI/logging; los consumidores, relay y jobs se implementan en Fase 6. Detener API y Worker con Ctrl+C.

Esta prueba crea PostgreSQL temporal con Testcontainers, aplica las migraciones reales mediante Flyway y consulta con un login restringido. Docker debe estar encendido; no requiere Compose ni modifica la base local. Comprueba todas las columnas/vistas, defaults e IDs, perfil obligatorio, permisos, JSON, clave compuesta y persistencia/búsqueda por coseno de vectores.

## Solución (F2-01)

```powershell
dotnet restore
dotnet build -warnaserror
dotnet format --verify-no-changes
```

Los cinco proyectos de pruebas usan xUnit v3 y Microsoft.Testing.Platform. No hay endpoints de negocio todavía.

F2-04 incorpora `Result<T>`/`Error`, contratos de comandos y queries y sus decoradores FluentValidation. Registrar futuros handlers con `AddCommandHandler` / `AddQueryHandler` y sus `IValidator<T>` para pasar siempre por validación. `IClock` devuelve UTC y usa `TimeProvider` inyectado. `IUnitOfWork.ExecuteInTransactionAsync` confirma solo un Result exitoso y revierte ante fallo/excepción/cancelación; no admite transacciones anidadas. No incluye reintentos automáticos: las futuras operaciones que mezclen pagos/efectos externos deben controlar su idempotencia antes de reintentar la transacción.

```powershell
dotnet test --project tests/Promotora.Domain.Tests
dotnet test --project tests/Promotora.Application.Tests
dotnet test --project tests/Promotora.Infrastructure.Tests
```
