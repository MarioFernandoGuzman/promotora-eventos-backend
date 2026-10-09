<!--
El TÍTULO del PR debe seguir Conventional Commits, porque con squash-merge
ese título es el que queda en el historial de develop.
Ej.: feat(ticketing): reservar cupo de forma atómica
-->

## ¿Qué cambia y por qué?
<!-- 2-4 líneas. El porqué importa más que el qué. -->

## Tareas del plan
<!-- IDs de docs/PLAN_IMPLEMENTACION.md -->
Closes: F_-__

## Cómo se probó
<!-- Pruebas añadidas, comandos ejecutados, evidencia (captura, log, resultado de carga). -->
- [ ] `dotnet build -warnaserror`
- [ ] `dotnet test`
- [ ] `dotnet format --verify-no-changes`

## Reglas críticas (AGENTS.md §5)
- [ ] No hay lectura-comparación-escritura de cupo (reserva atómica)
- [ ] No hay `DELETE` sobre compras, boletos, pagos, ledger ni logs
- [ ] Nada se publica a RabbitMQ dentro de la transacción (se usa outbox)
- [ ] Consumidores idempotentes / `Idempotency-Key` respetado
- [ ] Se verifica propiedad (dueño/cliente), no solo rol
- [ ] Se registra auditoría donde corresponde
- [ ] Fechas en UTC con `IClock` y `CancellationToken` en operaciones async
- [ ] Controllers sin `DbContext`, RabbitMQ ni OpenAI directos

## Base de datos
- [ ] No aplica
- [ ] Nueva migración `V<n>__*.sql` (no se editó ninguna migración ya aplicada)
- [ ] Cambios en esquema `bi` van en migración

## Definition of Done
- [ ] Casilla(s) marcada(s) en `docs/PLAN_IMPLEMENTACION.md`
- [ ] Endpoints documentados en Swagger
- [ ] Sin secretos, `bin/`, `obj/` ni archivos ajenos a la tarea
- [ ] Commits en Conventional Commits con `Refs: <ID>`

## Notas para quien revisa
<!-- Decisiones de diseño, riesgos, cosas que quieres que se miren con atención. -->
