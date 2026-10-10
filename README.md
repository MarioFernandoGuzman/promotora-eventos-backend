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
| [`docs/RECINTOS_LOCALIDADES_MAPA.md`](docs/RECINTOS_LOCALIDADES_MAPA.md) | CRUD de recintos/localidades, permisos, capacidades y mapa público |
| [`docs/CATALOGO_EVENTOS.md`](docs/CATALOGO_EVENTOS.md) | Categorías, presentadores, eventos, tipos de entrada y catálogo público para frontend |
| [`docs/VALIDACION_F4_CATALOGO_EVENTOS.md`](docs/VALIDACION_F4_CATALOGO_EVENTOS.md) | Pruebas y revisión del bloque F4-04–F4-08 |
| [`docs/IMAGENES.md`](docs/IMAGENES.md) | Subida de planos/fotos, multipart en Swagger, permisos y almacenamiento local |
| [`docs/VALIDACION_FASE_4.md`](docs/VALIDACION_FASE_4.md) | Cierre de catálogo/mapa/imágenes, auditoría y propiedad; 120 pruebas |
| [`docs/COMPRAS_DOMINIO.md`](docs/COMPRAS_DOMINIO.md) | F5-01: reglas de compra, política monetaria D-15, estados, configuración y pruebas |
| [`docs/RESERVA_CUPO.md`](docs/RESERVA_CUPO.md) | F5-02: reserva atómica, devolución idempotente y pruebas PostgreSQL |
| [`docs/COMPRAS_API.md`](docs/COMPRAS_API.md) | F5-03: crear compras desde Swagger, permisos, idempotencia y pruebas locales |
| [`docs/PAGOS_SIMULADOS.md`](docs/PAGOS_SIMULADOS.md) | F5-04: pasarela simulada, referencia idempotente, configuración y límites |
| [`docs/BOLETOS_EMISION.md`](docs/BOLETOS_EMISION.md) | F5-05: emisión interna, firma HMAC, carreras, clave externa y pruebas |
| [`docs/VALIDACION_FASE_3.md`](docs/VALIDACION_FASE_3.md) | Pruebas y revisión de salida de identidad/seguridad |

## Arranque rápido

Fases 2, 3 y 4 implementadas: solución por capas, persistencia, identidad/seguridad, recintos/localidades/mapa, catálogo de eventos con tipos de entrada e imágenes. Preparar variables de entorno, login restringido, clave JWT y bootstrap siguiendo [`docs/DESARROLLO_LOCAL.md`](docs/DESARROLLO_LOCAL.md).

```bash
docker compose up -d --wait
docker compose run --rm flyway migrate
dotnet run --project src/Promotora.WebApi
```

Swagger local: `http://localhost:5080/swagger`, con 59 operaciones: 11 de identidad, 11 de recintos/localidades/mapa, 27 de catálogo, 4 de imágenes, 4 de compras y 2 de boletos. El administrador inicial debe cambiar su contraseña antes de gestionar recursos. El catálogo público se consulta sin token en `/api/v1/catalogo/eventos`; la gestión requiere sesión y permisos. Como CLIENTE se puede reservar, consultar compras/boletos propios y cancelar una compra PENDIENTE. Administrador/dueño del recinto puede validar un boleto firmado una sola vez. Guía local/frontend: [BOLETOS_VALIDACION.md](docs/BOLETOS_VALIDACION.md). Los componentes internos de pago/emisión F5-04/F5-05 están implementados; su conexión HTTP espera el registro financiero transaccional de F7. El Worker es un host preparado; el procesamiento asíncrono llega en Fase 6.

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
