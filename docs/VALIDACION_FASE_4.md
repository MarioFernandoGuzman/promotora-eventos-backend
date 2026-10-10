# Cierre de Fase 4 — Recintos, catálogo e imágenes

F4-01–F4-10 implementadas. Último bloque F4-09/F4-10 en `feature/f4-imagenes-cierre`, desde `origin/develop` en `c1584ad` (PR #13 integrado). Un commit por tarea, en español, con `Refs`. El agente prepara commits locales y el PR; el usuario publica y mergea.

## Criterio de salida y módulos

El criterio del plan es: «un dueño puede crear recinto, zonas, evento y tipos de entrada; el público ve catálogo y mapa». La prueba `DuenoCompletaCatalogoYMapaPublicosConAuditoriaSinCambiosDelPropietarioAjeno` recorre todo ese flujo mediante HTTP y PostgreSQL real: recinto propio, localidad con polígono, evento BORRADOR, tipo/precio/cupo por localidad, plano/foto, PROGRAMADO → EN_VENTA y consultas anónimas de catálogo, detalle, mapa e imágenes. Verifica geometría, precio/cupo, URLs, actor y fecha UTC de cada auditoría. Cuatro modificaciones de otro dueño devuelven 403, sin modificar recursos ni crear auditoría de CRUD.

| Módulo | Implementación | Evidencia |
|---|---|---|
| Venues | Recintos/localidades, capacidades, bajas lógicas, mapa público | Propiedad, geometría, límites, carreras, baja idempotente y auditoría |
| Catalog | Categorías/presentadores, eventos, asociaciones, tipos, listado/detalle público | RBAC, ventanas UTC, estado esperado, reservas conservadas, filtros y paginación |
| Images | Tres subidas multipart y lectura pública filtrada, puerto de almacenamiento local | PNG/JPEG reales, MIME/tamaño/dimensiones, nombres generados, visibilidad, rollback |
| Audit / Identity | Actor de sesión, usuario activo, permiso por propietario y auditoría en transacción | Acceso ajeno denegado; fallo de auditoría revierte URL y elimina archivo nuevo conservando el anterior |

Los estados y reglas puras siguen en Domain; puertos y handlers en Application; EF y almacenamiento en Infrastructure; controllers delegan. Swagger contiene **53 operaciones**: identidad 11, recintos/localidades/mapa 11, catálogo 27 e imágenes 4. La suite comprueba selector multipart `archivo`, Bearer y esquema de descarga PNG binaria. Worker no cambia.

## Validación final local

Comandos desde la raíz, por separado, con Docker encendido:

```powershell
dotnet restore
```

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Build Release: 0 warnings/errores. Suite: **120 aprobadas, 0 fallidas, 0 omitidas** (Domain 21, Application 4, Infrastructure 29, API 54, Architecture 12). Se añaden 9 respecto al bloque anterior: 4 de almacenamiento y 5 de API. PostgreSQL+pgvector/Flyway y permisos del rol restringido se prueban con Testcontainers; los archivos usan directorios temporales independientes. Evidencia TRX en `TestResults/`, ignorada por Git.

Se verificó también Compose/Flyway, Kestrel, health checks, Swagger UI/documento, bootstrap y cambio inicial, RabbitMQ management y MailHog. El script `scripts/Test-Compose.ps1 -WithApi` encontró 5432 ocupado por Compose local; se ejecutó una variante temporal en memoria con puertos libres, mismos servicios/credenciales efímeras y datos tmpfs. Pasó sin detener el entorno local ni cambiar el script versionado. Se retiraron únicamente los recursos de esa prueba. El límite de transporte de subida no forma parte de este smoke test.

## Revisión del diff de fase contra AGENTS.md §§5/12

Se revisaron los cambios de F4 desde `6847b62`, más el cierre, contra el alcance y las reglas de dependencia, ownership, auditoría y persistencia.

- Cada escritura verifica usuario/rol/propiedad en Application. Las escrituras bloquean y revalidan recursos; baja/cambio de estado usan predicados condicionales. La auditoría se confirma en la misma transacción y las bajas repetidas no la duplican.
- Cupo de tipos se configura por diferencia con UPDATE condicional; no se restaura una reserva desde un valor leído. La venta y su reserva atómica llegan en F5; no se declara todavía implementada la compra.
- Bajas lógicas de recintos, localidades, categorías, presentadores y tipos; cancelación de eventos. Solo la relación N:M de presentadores admite borrado físico, según BASE_DE_DATOS.md. Sin DELETE de usuarios, compras, boletos, pagos, ledger o logs.
- No hay cambios a Flyway, tablas, columnas, roles PostgreSQL ni esquema bi. Las 26 tablas/16 vistas se conservan. Ninguna migración commiteada se edita ni se ejecuta el esquema histórico.
- Domain/Application no conocen EF ni almacenamiento físico; controllers no usan DbContext, RabbitMQ ni OpenAI. No hay DateTime.Now, bloqueo async, secretos ni archivos subidos en Git.
- Nuevos paquetes: SkiaSharp y sus binarios Linux sin dependencias, 3.119.4, MIT. Se justifican para decodificar y normalizar imágenes; licencias/versiones en [DEPENDENCIAS.md](DEPENDENCIAS.md). No se introducen librerías con licencia comercial.
- Reglas de dinero, outbox, consumidores, QR, reseñas y RAG conservan sus tareas futuras. Este bloque no modifica esos flujos.

**Incumplimientos identificados en el alcance de Fase 4: ninguno.** CI remoto queda por confirmar al publicar el PR. El cierre local no implica despliegue ni venta de boletos.

## Limitaciones y prueba manual

El almacenamiento local necesita disco persistente y respaldo junto a BD. Una caída abrupta puede dejar archivos huérfanos; los reemplazos conservan físicamente los anteriores. No se sirven cuando pierden su referencia pública. Limpieza automática y varias réplicas quedan fuera de F4-09. EXIF/orientación no se conserva al normalizar. El límite de cuerpo de Kestrel queda para prueba manual en un host real; TestServer no implementa esa característica, aunque sí se prueba el límite real de lectura de 5 MiB.

La cancelación con compras devuelve 409 hasta implementar el reembolso de F7. Precios públicos son base; general/distancias tienen cupo por tipo y el límite agregado definido es por localidad. No se añaden nuevas reglas o cambios de esquema.

Arranque y configuración: [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md). Autenticación: [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). Flujo de recinto/mapa: [RECINTOS_LOCALIDADES_MAPA.md](RECINTOS_LOCALIDADES_MAPA.md). JSON/estados: [CATALOGO_EVENTOS.md](CATALOGO_EVENTOS.md). Prueba de subida en Swagger y contrato frontend: [IMAGENES.md](IMAGENES.md).

Reiniciar API, abrir Swagger, login/cambio inicial y Authorize con accessToken. Como dueño, crear recinto/localidad/evento BORRADOR/tipo y subir plano/foto; como admin, presentador e imagen. Asociarlo antes de publicar. Pasar a PROGRAMADO y EN_VENTA; quitar token para consultar catálogo/mapa/imágenes. Probar otro dueño → 403, SVG/truncado/grande → 400, borrador oculto → 404, cambio con estado anterior → 409. Restaurar Authorize para continuar gestionando.
