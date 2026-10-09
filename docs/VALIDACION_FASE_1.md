# Cierre de Fase 1 — PR-D

Rama: `feature/f1-integridad-bi-seed`, desde develop con PR-B y PR-C integrados. Tareas de este PR: F1-07, F1-11, F1-08, F1-09 y F1-10. Validación local: 2026-10-08, PostgreSQL 16.15 y Flyway 13.10.0. Las imágenes conservan los tags del CI (`pgvector/pgvector:pg16` y `flyway/flyway:latest`).

## Resultado y reproducción

```powershell
pwsh -File .\database\seed-dev\validar-fase1.ps1
```

El comando trabaja exclusivamente en un contenedor temporal, sin puertos publicados ni volúmenes persistentes. Aplica desde cero las diez migraciones versionadas y la repetible de BI. Usa el comando Flyway del CI, adaptando únicamente red y montaje a Windows. Al finalizar retira solo su propio contenedor.

| Comprobación | Resultado |
|---|---|
| Flyway migrate desde cero | V1–V10 y R__bi_vistas.sql: Success |
| Segundo migrate | No migration necessary |
| Flyway validate / info | 11 migraciones válidas, ninguna pendiente ni fallida |
| Diccionario contra PostgreSQL | 26 tablas, 198 columnas y 104 restricciones coincidentes |
| pgvector | Extensión y HNSW con vector_cosine_ops presentes |
| Perfiles | Cuatro constraint triggers DEFERRABLE INITIALLY DEFERRED; perfil ausente, incompatible o múltiple rechazado; cambio de rol transaccional válido |
| Capacidad y coherencia | Exceso de cupo, reducción de capacidad y reasignaciones de padres incompatibles rechazados |
| Inmutabilidad | UPDATE/DELETE rechazados en ledger y ambos logs |
| Finanzas | Cuentas únicas por moneda; movimientos en CERRADA rechazados; importes y reembolsos conservan signo |
| Permisos | promotora_app permite DML autorizado; bi_reader consulta las 16 vistas y no las tablas operativas; flyway permite DDL y acceso a su historial |
| BI | Totales sin duplicación por detalles/pagos; monedas separadas; fechas UTC; reseñas pendientes y boletos anulados excluidos donde corresponde |
| Producción | Seis categorías y dos cuentas GTQ ACTIVA con saldo cero; cero usuarios y movimientos |
| Desarrollo | Seed repetido sin duplicados: dos usuarios inactivos, un recinto, dos localidades, dos eventos y cuatro tipos |
| Concurrencia | Capacidad en READ COMMITTED y REPEATABLE READ, cupo por localidad y cierre contra movimiento: un ganador y un rechazo en cada carrera |

## Módulos disponibles

El esquema ya cubre Identity, Venues, Catalog, Ticketing, Finance, Reviews, AI, Audit/Messaging y Reporting/BI. Disponibilidad en Fase 1 significa tablas, restricciones y vistas; no hay todavía servicios C#, endpoints ni procesos de negocio.

El seed permite inspeccionar el catálogo por localidad y por distancia de carrera. Las 16 vistas preparan las fuentes para los 15 reportes; sus filtros finales, endpoints, exportaciones y Power BI se implementarán en F10. Swagger UI llegará en F2-05, después de crear la solución en F2-01.

## Revisión contra salida del plan y AGENTS §§5/12

El criterio de salida de Fase 1 se cumple: esquema completo creado solo con migraciones, vistas BI consultables y diccionario completo contrastado con PostgreSQL. No se alteraron migraciones previamente commiteadas, no se añadieron tablas/columnas operativas fuera del modelo, no se introdujeron paquetes ni otro backend, y no se versionaron secretos o credenciales de bootstrap. Los datos de prueba están fuera de las migraciones de producción. No se ejecutó el script histórico ni comandos destructivos contra una BD existente.

Incumplimientos funcionales del alcance de Fase 1: ninguno detectado por esta revisión y estas pruebas. Incumplimiento de formato: `git diff develop...HEAD --check` informa una línea vacía adicional al final de `V9__indices_triggers_permisos.sql`; no se modifica porque la migración ya está commiteada. Su checksum y ejecución son válidos.

Las reglas de sobreventa en compras, idempotencia de mensajes, QR firmado, autorización por propiedad, auditoría desde handlers y conciliación financiera transaccional se implementarán y probarán en sus fases respectivas. Las pruebas actuales de concurrencia validan las protecciones SQL de F1, sin afirmar que el flujo de compra esté implementado. V9 no ajusta saldos automáticamente ni genera cobros. La facturación electrónica sigue fuera de alcance.

No existen solución .NET ni docker-compose.yml todavía. Por ello build, dotnet test, format y NetArchTest no aplican en esta fase. El CI actual aplica/valida migraciones; las pruebas SQL adicionales se ejecutan con el validador local anterior. El estado del CI remoto se comprobará al abrir el PR.

## Entrega

Commits locales convencionales, en español y con Refs por tarea. El agente no hizo push, merge ni abrió el PR. El usuario publica esta rama y crea el PR hacia develop. Fase 2 comienza después de integrar PR-D y actualizar develop.
