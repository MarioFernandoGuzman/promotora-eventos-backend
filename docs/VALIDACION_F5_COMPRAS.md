# Revisión del bloque F5-01 a F5-03

Rama feature/f5-dominio-compras, basada en origin/develop c1966c0. La decisión D-15 se registró en un commit documental anterior al código de F5-01. Se incluye el fix de sentinels de DebeCambiarPassword/Publicado sin cambiar el esquema ni suprimir warnings.

## Evidencia local

| Verificación | Resultado |
|---|---|
| dotnet build --no-restore -warnaserror -c Release | 0 errores, 0 warnings |
| dotnet test --no-build -c Release --report-trx | 159 aprobadas, 0 fallidas, 0 omitidas |
| Distribución | Domain 33; Application 5; Infrastructure 38; API 71; Architecture 12 |
| dotnet format --verify-no-changes --no-restore | Limpio |
| Swagger | 54 operaciones; compra con header obligatorio, Bearer, ejemplo y códigos de error |
| PostgreSQL real | pgvector/pgvector:pg16 + Flyway en contenedores temporales con rol restringido |

Los resultados TRX permanecen en TestResults, ignorado por Git. CI remoto se comprueba al publicar el PR.

## Revisión contra AGENTS.md §§5/12

- Reserva mediante UPDATE condicional por evento/tipo/actividad/cupo, sin lectura-comparación-escritura. Concurrencia: 20 solicitudes sobre 5 lugares producen 5 reservas.
- Devolución condicional de estado en la misma transacción; 20 cancelaciones/expiraciones producen un ganador. Rollback restaura ambos efectos y se suman detalles repetidos.
- Idempotencia propia devuelve compra original sin recalcular ni reservar; carrera entre clientes no revela compra ajena y produce una sola reserva/compra. El UNIQUE existente mantiene la garantía.
- Usuario/rol/estado/password y propiedad se comprueban en el handler; compras solo con perfil CLIENTE. Precios del catálogo, evento único y máximo agregado.
- Importes D-15 con decimal, redondeo por componente y congelación; pruebas de montos conocidos, aleatorios, límites y gratuidad. Este bloque no escribe ledger.
- Compra/detalles/reserva/auditoría se confirman juntos. Fallo de auditoría revierte toda la operación.
- Fechas UTC desde IClock; CancellationToken propagado, controllers con handlers y sin infraestructura directa. Arquitectura verificada por la suite.
- Sin DELETE, cambios de migraciones/bi, nuevas tablas/columnas, paquetes, secretos, binarios ni ejecución del script histórico. Sin publicación de mensajes dentro de la transacción.

No se detectaron incumplimientos del alcance de F5-01–F5-03. El criterio de salida de Fase 5 todavía no se cumple: faltan F5-04 a F5-10 (pago, emisión/QR, consultas/cancelación, validación en puerta, expiración y pruebas finales del flujo completo). La transición a compra asíncrona/outbox/202 pertenece a F6.

## Decisiones de implementación

Reserva inicial configurable de 15 minutos y moneda GTQ del catálogo actual. Rate limit inicial configurable de 20 solicitudes/minuto/IP. Creación HTTP 201 y reutilización HTTP 200 mientras se completa Fase 5. Esta etapa conserva compras PENDIENTE y aún no ejecuta expiración automática; la guía local lo indica explícitamente. No se publica Location a un GET que todavía no está implementado.

Bloqueos compartidos sobre padres estabilizan catálogo sin serializar todos los compradores. Se bloquean tipos en orden antes de congelar precios; la reserva sigue usando UPDATE condicional. Deadlock/serialización y UNIQUE de clave reintentan toda la transacción, hasta tres intentos, revalidando al usuario en cada uno.

Guía de integración/pruebas: [COMPRAS_API.md](COMPRAS_API.md). Dominio: [COMPRAS_DOMINIO.md](COMPRAS_DOMINIO.md). Adaptador de cupo: [RESERVA_CUPO.md](RESERVA_CUPO.md).
