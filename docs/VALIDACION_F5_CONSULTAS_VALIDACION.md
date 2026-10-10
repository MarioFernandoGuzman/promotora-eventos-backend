# Revisión F5-06/F5-07

Base: origin/develop 267e235 (PR #16 integrado). Rama: feature/f5-consultas-validacion.

## Resultado

- F5-06: consultas propias paginadas de compras/boletos con QR; cancelación PENDIENTE → CANCELADA, devolución única y auditoría.
- F5-07: validación HMAC y autorización por recinto; entrada atómica VALIDO → USADO con fecha UTC, validador y auditoría.
- Cinco operaciones nuevas: Swagger pasa de 54 a 59. No se añadió endpoint de emisión/finalización.
- Sin cambios de esquema, paquetes, credenciales ni migraciones.

## Comprobaciones ejecutadas

`dotnet build --no-restore -warnaserror -c Release`: cero advertencias/errores.

`dotnet test --no-build -c Release --report-trx`: 204 aprobadas, cero fallos/omisiones: 39 Domain, 5 Application, 60 Infrastructure, 88 API, 12 Architecture. F5-06 pasó su suite de 195 pruebas antes de su commit; la suite final incorpora nueve casos de F5-07.

`dotnet format --verify-no-changes --no-restore`: limpio. `git diff --check`: sin errores.

Pruebas nuevas contra PostgreSQL real temporal, Flyway y rol de aplicación restringido: propiedad de compras/boletos/recintos, paginación, datos sin caché, sesión/rol también en handlers, estado inactivo, veinte cancelaciones con devolución única, veinte escaneos con dos administradores y una sola entrada, firma alterada, falta de clave, USADO/ANULADO, compra no completada, rollback de auditoría y contrato Swagger. Sin modificar Compose.

## AGENTS.md §§5 y 12

La cancelación reutiliza el UPDATE condicional de F5-02 y solo su ganador devuelve cupo. Repetir CANCELADA no duplica devolución/auditoría; PROCESANDO responde 409. Compras ajenas son 404, las listas se filtran por cliente y no se aceptan identificadores de actor proporcionados por el solicitante.

La validación verifica la firma y propiedad antes del UPDATE condicional. Recinto/evento/compra quedan bloqueados en la transacción para estabilizar la propiedad y el estado. Solo una transición ganadora fija fecha_uso y validado_por. La fecha de respuesta usa la precisión de microsegundos de PostgreSQL. Auditoría y modificación confirman juntas; ante fallo revierten juntas. No se devuelve QR ni información del asistente al validador.

Los controllers solo invocan handlers. Application define puertos y no conoce EF/Infrastructure. Operaciones async reciben CancellationToken; tiempo mediante IClock. Sin borrados, publicación RabbitMQ, escritura de pagos/ledger, alteración de migraciones o esquema bi. Ninguna nueva clave/contraseña se genera para el entorno local.

No se detectaron incumplimientos dentro de F5-06/F5-07. La salida de Fase 5 sigue pendiente: faltan F5-08/F5-09/F5-10 y la conexión financiera de F7-02/F7-03. POST /compras todavía reserva PENDIENTE sin cobro/emisión HTTP. La validación exitosa se verifica hoy con fixtures de boletos gratuitos; la guía no pide forzar SQL sobre compras locales. No se añadió una ventana de entrada por fecha sin una regla del plan.

Guías: [COMPRAS_API.md](COMPRAS_API.md), [BOLETOS_VALIDACION.md](BOLETOS_VALIDACION.md) y [BOLETOS_EMISION.md](BOLETOS_EMISION.md).
