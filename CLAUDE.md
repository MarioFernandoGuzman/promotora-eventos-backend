@AGENTS.md

# CLAUDE.md — Instrucciones específicas para Claude Code

Todo lo del proyecto (stack, arquitectura, reglas de negocio, límites, Definition of Done) está en `AGENTS.md`, importado arriba. **No lo dupliques aquí.** Este archivo solo añade cómo trabajar en este repositorio con Claude Code.

## Flujo de trabajo por tarea

1. **Ubica la tarea.** Abre `docs/PLAN_IMPLEMENTACION.md` y toma la siguiente tarea sin marcar de la fase activa. No saltes de fase sin que el criterio de salida de la anterior esté cumplido.
2. **Consulta el modelo de datos.** Antes de escribir entidades, repositorios o SQL, lee la sección correspondiente de `docs/BASE_DE_DATOS.md`. No inventes columnas ni tablas.
3. **Planifica primero** (modo plan) cuando la tarea toque más de 3 archivos, el esquema de BD, la mensajería o seguridad. Presenta el plan y espera confirmación.
4. **Implementa en incrementos pequeños**: Domain → Application (puertos + handler) → Infrastructure (adaptador) → WebApi (endpoint) → pruebas. Una capa a la vez, compilando entre pasos.
5. **Escribe la prueba junto con el código.** Para cualquier cambio en compras, cupo, finanzas o mensajería, la prueba de concurrencia/idempotencia es obligatoria.
6. **Verifica** (ver checklist) y **marca la casilla** de la tarea en `docs/PLAN_IMPLEMENTACION.md` en el mismo commit/PR.

## Checklist antes de cada commit

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
git status        # sin secretos, sin bin/ obj/, sin archivos ajenos a la tarea
```

- Mensaje en Conventional Commits, con la tarea en el cuerpo (`Refs: F5-03`).
- Trabaja en una rama `feature/<fase>-<descripcion>`; nunca commitees directo a `main` ni a `develop`.

## Cómo debe comportarse Claude en este repo

- **Pregunta antes de asumir** si una decisión afecta al modelo de datos, a las reglas financieras (comisión, IVA, reembolsos) o a la seguridad. Las decisiones abiertas están en la tabla de decisiones del plan.
- **Cambios mínimos y enfocados.** No refactorices código ajeno a la tarea ni "mejores" migraciones ya aplicadas.
- **Si un comando de `AGENTS.md` no existe todavía**, dilo y revisa en qué fase se crea; no lo simules.
- **No ejecutes** comandos destructivos (`DROP`, `git reset --hard`, `git push --force`, `docker compose down -v`) sin confirmación explícita.
- **Secretos:** nunca pidas ni escribas las API keys reales de Ollama y Gemini en archivos versionados; el usuario introduce OLLAMA_API_KEY y GEMINI_API_KEY en .env local ignorado o configuración externa. Ver D-05 y docs/IA_OLLAMA.md.
- **Explica el porqué** de las decisiones de diseño en el PR; el proyecto se defenderá oralmente y el historial debe ser entendible.

## Revisión (antes de abrir el PR)

Haz una pasada de revisión sobre tu propio diff buscando específicamente:
- ¿Algún controller toca `DbContext`, RabbitMQ u Ollama directamente? (violación de arquitectura)
- ¿Hay lectura-comparación-escritura de cupo en vez de `UPDATE` atómico?
- ¿Algún `DELETE` sobre tablas financieras, de compras o de logs?
- ¿Se publica a RabbitMQ dentro de la transacción en lugar de usar outbox?
- ¿Falta verificar propiedad (dueño/cliente) o auditoría en la operación?
- ¿Se usa `DateTime.Now` o falta `CancellationToken`?

## Contexto y sesiones

- Trabaja **una fase por sesión** y limpia el contexto (`/clear`) al cambiar de fase; el estado real vive en el plan y en git, no en la conversación.
- Al retomar, empieza leyendo `docs/PLAN_IMPLEMENTACION.md` y `git log --oneline -20`.
