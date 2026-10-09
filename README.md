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

## Arranque rápido

> Disponible al terminar la Fase 2 del plan.

```bash
docker compose up -d
docker compose run --rm flyway migrate
dotnet run --project src/Promotora.WebApi
```

## Flujo de trabajo

- Ramas: `main` (protegida) · `develop` · `feature/<fase>-<descripcion>`
- Commits en [Conventional Commits](https://www.conventionalcommits.org/) con `Refs: <ID de tarea>`
- Todo cambio entra por Pull Request hacia `develop`
