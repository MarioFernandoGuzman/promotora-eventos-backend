# Promotora de Eventos — Backend

Plataforma para una promotora de eventos (conciertos, partidos, vigilias, cultos, carreras 5K/10K/21K/42K y meet & greets): venta de boletos por localidad o entrada general con colas asíncronas, finanzas, reseñas, mapas de recintos, agente de IA con RAG, reportería y BI.

**Estado:** en construcción. El avance se sigue en [`docs/PLAN_IMPLEMENTACION.md`](docs/PLAN_IMPLEMENTACION.md).

## Stack

C# / .NET 10 (Clean Architecture) · ASP.NET Core Web API · PostgreSQL 16 + pgvector · EF Core · Flyway · RabbitMQ · Ollama Cloud (chat) + Gemini API (embeddings) · Docker Compose · GitHub Actions

## Documentación

| Documento | Contenido |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Reglas del proyecto para agentes de IA y desarrolladores |
| [`CLAUDE.md`](CLAUDE.md) | Instrucciones específicas para Claude Code |
| [`docs/BASE_DE_DATOS.md`](docs/BASE_DE_DATOS.md) | Modelo de datos v2 |
| [`docs/PLAN_IMPLEMENTACION.md`](docs/PLAN_IMPLEMENTACION.md) | Fases, tareas y trazabilidad de requerimientos |
| [`docs/IA_RAG.md`](docs/IA_RAG.md) | Arranque, carga/indexación, chat con fuentes y límites |
| [`docs/ANALISIS_FRONTEND.md`](docs/ANALISIS_FRONTEND.md) | Pantallas, roles, contratos, flujos web y pendientes |
| [`docs/ENDPOINTS.md`](docs/ENDPOINTS.md) | Inventario de 80 operaciones de Swagger |
| [`docs/VALIDACION_FASE_9.md`](docs/VALIDACION_FASE_9.md) | Cierre IA/RAG, 524 pruebas y evaluación acotada |
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

Backend de identidad, catálogo/mapas, compra asíncrona y boletos, finanzas simuladas, reseñas y agente público con RAG implementado hasta Fase 9. Reportes y Power BI se revisarán después. Frontend aún no creado; D-09 pendiente.

Con Docker Desktop y el .env privado ya configurado, ejecuta por separado en PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Api
```

Swagger local: http://localhost:5080/swagger, con 80 operaciones REST. El administrador inicial debe cambiar su contraseña antes de gestionar recursos. Para RAG carga documentos públicos e indexa explícitamente; la carga por sí sola queda PENDIENTE. Sigue [IA_RAG.md](docs/IA_RAG.md).

Las compras asíncronas requieren Worker y sus flags habilitados conscientemente; no hace falta ejecutarlo para catálogo/chat. La pasarela es simulada. Consulta [ANALISIS_FRONTEND.md](docs/ANALISIS_FRONTEND.md) para pantallas, permisos, polling y la configuración necesaria antes de probar ventas.
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
