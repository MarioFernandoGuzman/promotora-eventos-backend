# Validación de F4-04–F4-08

Rama `feature/f4-catalogo-eventos`, creada desde `origin/develop` en `170ca0d` (PR #12 integrado). Un commit por tarea, en español y con `Refs`. No hay push ni merge desde el agente.

## Módulos implementados

| Tarea | Comportamiento | Verificación principal |
|---|---|---|
| F4-04 | CRUD de categorías/presentadores con baja lógica | Admin escribe, cliente/dueño leen; duplicados, tipos y auditoría |
| F4-05 | Eventos con creador de sesión, ventana de venta y estados | Propiedad, UTC, transiciones condicionales, carrera y bloqueo con compras |
| F4-06 | Asociar presentadores y cambiar rol o retirar relación | PK compuesta, rol opcional, permisos, inactivo y retirada idempotente |
| F4-07 | General/localidad/distancia y configuración de precio/cupo | Cupo de localidad, inactivos, carrera entre actores y reservas conservadas |
| F4-08 | Catálogo y detalle públicos | Filtros, paginación, anonimato, datos privados omitidos y recursos ocultos |

Domain mantiene reglas de eventos y tipos de entrada; Application define casos de uso y puertos; Infrastructure implementa EF y transacciones; controllers solo delegan. Se reutilizan identidad, propiedad por recinto, validación y auditoría existentes. El Worker no cambia.

## Comandos y resultados

Con Docker operativo, desde la raíz:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

Resultado del bloque: build Release con 0 warnings/errores; 111 pruebas aprobadas, 0 fallidas y 0 omitidas; formato limpio. Domain 21, Application 4, Infrastructure 25, API 49 y Architecture 12. Las pruebas de API/infraestructura migran PostgreSQL+pgvector temporal y usan el rol restringido; no modifican Compose. Se añaden 18 pruebas respecto a la base de 93. Swagger generado contiene 49 operaciones y ejemplos de cuerpos para las escrituras del catálogo.

Los logs/TRX quedan en `TestResults/`, ignorado por Git. Durante la implementación se corrigieron traducción EF de la asociación y anonimato explícito por acción; la evidencia anterior es la ejecución final del código corregido.

## Revisión contra AGENTS.md §§5 y 12

- Reserva/compra no se implementan en este bloque. La configuración de cupo previa a ventas usa UPDATE condicional atómico por diferencia, sin sobrescribir reservas; su carrera está probada. No se publica a RabbitMQ ni se modifica el ledger.
- No hay borrado de usuarios, compras, boletos, pagos o logs. Categorías, presentadores y tipos usan baja lógica; eventos usan CANCELADO. Solo la asociación N:M se elimina físicamente, como permite BASE_DE_DATOS.md §11.
- Estados se actualizan con estado esperado. Propiedad, usuario activo y cambio obligatorio se comprueban en handlers, con bloqueos durante las escrituras. Auditoría y cambio se confirman en la misma transacción; las bajas repetidas no duplican auditoría.
- Fechas UTC, reloj inyectado y CancellationToken. Sin DateTime.Now, bloqueos síncronos o dependencias de infraestructura en Domain/Application/controllers.
- No cambian migraciones, esquema bi, tablas, columnas, paquetes ni secretos. Persistencia sigue siendo PostgreSQL real y Flyway. Los nuevos endpoints cubren CRUD y catálogo/detalle público previstos en el plan y AGENTS.md.

Incumplimientos identificados en el alcance F4-04–F4-08: ninguno. Las reglas financieras, QR, outbox y RAG conservan sus tareas futuras; esta revisión no declara que estén implementadas.

## Prueba local con Swagger

Con las credenciales locales ya configuradas, ejecutar cada comando por separado. Las contraseñas de Compose deben corresponder a los volúmenes existentes; no reconfigurar ni eliminar volúmenes para esta tarea.

```powershell
docker compose up -d --wait
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
docker compose run --rm flyway validate
```

```powershell
dotnet run --project src/Promotora.WebApi
```

Abrir [Swagger](http://localhost:5080/swagger). Si la API estaba abierta, detenerla primero con Ctrl+C. Iniciar sesión con el administrador configurado, completar cambio obligatorio si aplica y poner solo accessToken en Authorize. No se añade una contraseña de demostración al repositorio.

1. Como admin, crear categoría/presentador y alta de dueño; iniciar sesión con el dueño.
2. Crear recinto propio y localidad; copiar IDs de las respuestas.
3. Crear evento BORRADOR, configurar presentador y tipo de entrada. Usar fechas UTC y ventana que incluya la hora actual.
4. Cambiar BORRADOR → PROGRAMADO, después PROGRAMADO → EN_VENTA. Repetir la transición con el estado anterior devuelve 409; editar configuración en venta devuelve 409.
5. Quitar Authorize y consultar `/api/v1/catalogo/eventos` y detalle, aplicando filtros; la gestión privada sin token sigue devolviendo 401.
6. Abrir mapa con recinto.id y evento.id. Como otro dueño, la gestión del evento devuelve 403. Probar excesos de localidad (422), precio con tres decimales (400) y baja repetida de recursos configurables (200 sin duplicar auditoría).

Cuerpos JSON, rutas y casos de error en [CATALOGO_EVENTOS.md](CATALOGO_EVENTOS.md); acceso autenticado y CORS en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md).

## Alcance restante

F4-09 (subida de imágenes) y F4-10 (revisión formal de auditoría/propiedad y cierre de fase) siguen pendientes. El flujo recinto → localidad → evento → tipos → catálogo/mapa ya puede probarse, pero no se declara cerrada la Fase 4. La venta de boletos sigue en F5/F6 y los reembolsos en F7; cancelar un evento con compras se bloquea con 409 para no producir una cancelación financiera incompleta. Precios públicos son base, antes de los componentes de compra. Generales/distancias usan cupo por tipo; el límite agregado especificado en el modelo es por localidad.

GitHub Actions debe confirmar CI al publicar el PR. La validación aquí es local y no implica despliegue.
