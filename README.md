# Promotora de Eventos — Backend

Plataforma para una promotora de eventos (conciertos, partidos, vigilias, cultos, carreras 5K/10K/21K/42K y meet & greets): venta de boletos por localidad o entrada general con colas asíncronas, finanzas, reseñas, mapas de recintos, agente de IA con RAG, reportería y BI.

**Estado:** en construcción. El avance se sigue en [`docs/PLAN_IMPLEMENTACION.md`](docs/PLAN_IMPLEMENTACION.md).

## Stack

C# / .NET 10 (Clean Architecture) · ASP.NET Core Web API · PostgreSQL 16 + pgvector · EF Core · Flyway · RabbitMQ · OpenAI (LLM y embeddings) · Docker Compose · GitHub Actions

## Documentación

| Documento | Contenido |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Reglas del proyecto para agentes de IA y desarrolladores |
| [`CLAUDE.md`](CLAUDE.md) | Instrucciones específicas para Claude Code |
| [`docs/BASE_DE_DATOS.md`](docs/BASE_DE_DATOS.md) | Modelo de datos v2 |
| [`docs/PLAN_IMPLEMENTACION.md`](docs/PLAN_IMPLEMENTACION.md) | Fases, tareas y trazabilidad de requerimientos |
| [`docs/DESARROLLO_LOCAL.md`](docs/DESARROLLO_LOCAL.md) | Arranque seguro de Docker, login PostgreSQL, API/Swagger y pruebas |
| [`docs/DEPENDENCIAS.md`](docs/DEPENDENCIAS.md) | Versiones, propósito y licencias de NuGets |
| [`docs/IDENTIDAD_SEGURIDAD.md`](docs/IDENTIDAD_SEGURIDAD.md) | Usuarios, bootstrap, JWT, permisos, auditoría y pruebas en Swagger |
| [`docs/FRONTEND_SWAGGER_AUTENTICACION.md`](docs/FRONTEND_SWAGGER_AUTENTICACION.md) | Guía frontend: login en Swagger, Bearer, cambio inicial, refresh, logout y CORS |
| [`docs/VALIDACION_FASE_3.md`](docs/VALIDACION_FASE_3.md) | Pruebas y revisión de salida de identidad/seguridad |

## Arranque rápido

Fases 2 y 3 implementadas: solución por capas, persistencia, API e identidad/seguridad. Preparar variables de entorno, login restringido, clave JWT y bootstrap siguiendo [`docs/DESARROLLO_LOCAL.md`](docs/DESARROLLO_LOCAL.md).

```bash
docker compose up -d --wait
docker compose run --rm flyway migrate
dotnet run --project src/Promotora.WebApi
```

Swagger local: `http://localhost:5080/swagger`, con 11 operaciones de identidad y autorización Bearer. El administrador inicial debe cambiar su contraseña antes de gestionar usuarios. El Worker es un host preparado; el procesamiento de compras llega en Fase 6.

```bash
dotnet restore
dotnet build -warnaserror -c Release
dotnet test --no-build -c Release --report-trx
dotnet format --verify-no-changes
```

Docker debe estar encendido para las pruebas PostgreSQL/Testcontainers. Las pruebas crean bases temporales, sin modificar datos de Compose. Arquitectura forma parte de la suite; también puede verificarse con `dotnet test --project tests/Promotora.Architecture.Tests`.

## Flujo de trabajo

- Ramas: `main` (protegida) · `develop` · `feature/<fase>-<descripcion>`
- Commits en [Conventional Commits](https://www.conventionalcommits.org/) con `Refs: <ID de tarea>`
- Todo cambio entra por Pull Request hacia `develop`
