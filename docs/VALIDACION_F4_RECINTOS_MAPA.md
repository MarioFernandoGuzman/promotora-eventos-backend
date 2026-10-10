# Validación de F4-01–F4-03

Rama: `feature/f4-recintos-localidades-mapa`, desde `origin/develop` con PR #11 de identidad integrado. Incluye los dos commits locales de documentación pendientes de F3: generación de JWT compatible con PowerShell 5.1 y guía de Swagger para frontend. No se hizo push ni merge.

## Alcance

- F4-01: CRUD de recintos, permisos por rol/propiedad, paginación y baja lógica idempotente.
- F4-02: CRUD de localidades, geometría/color, capacidad del recinto y cupos ya asignados por evento.
- F4-03: mapa público del recinto activo y disponibilidad por evento; excluye datos de identidad y financieros.

Se añaden 11 operaciones a las 11 de identidad: 22 en Swagger. La fase no está terminada: catálogo, eventos, tipos de entrada, subida de imágenes y cierre global de auditoría/pruebas F4-04–F4-10 siguen pendientes. Las mutaciones de este bloque ya se auditan y tienen pruebas de propiedad.

## Comandos y resultados

```powershell
dotnet build --no-restore -warnaserror -c Release
```

Resultado: cero errores y cero advertencias.

```powershell
dotnet test --no-build -c Release --report-trx
```

Resultado final: 93 pruebas, cero fallos y cero omitidas. PostgreSQL real `pgvector/pgvector:pg16`, Flyway y JWT reales en pruebas HTTP; no se usa SQLite ni la base local como fixture de integración.

| Proyecto | Pruebas correctas |
|---|---:|
| Domain | 15 |
| Application | 4 |
| Infrastructure | 25 |
| API | 37 |
| Architecture | 12 |
| Total | 93 |

```powershell
dotnet format --verify-no-changes --no-restore
```

Resultado: formato limpio.

Docker Desktop estaba detenido al iniciar la validación; se inició y se repitieron las pruebas con el daemon operativo. Una prueba previa de readiness heredaba el DbContext configurado por los user-secrets locales: se aisló ApiFactory con conexión inalcanzable de pruebas, manteniendo DI válida. IdentityApiFactory usa exclusivamente PostgreSQL temporal para los contratos de negocio. No se cambian ni se leen contraseñas del entorno local.

## Evidencia funcional

- Dueño solo accede a sus recintos/localidades; administrador gestiona todos y CLIENTE no los gestiona.
- Las bajas usan UPDATE condicionado por activo=true; repetirlas no repite la auditoría ni borra historial.
- Al editar capacidad se incluyen localidades inactivas. La reducción no puede quedar por debajo de los cupos asignados por evento.
- Carrera de dos actores: dueño y administrador crean simultáneamente dos zonas de 60 sobre capacidad 100. Una petición responde 200 y la otra 422; persisten una zona de 60 y una auditoría CREATE.
- JSONB conserva type/points; color se normaliza a #RRGGBB. Campos inválidos devuelven ProblemDetails.
- Mapa anónimo excluye zonas/tipos inactivos, eventos BORRADOR/CANCELADO/FINALIZADO, evento ajeno y recinto inactivo. Devuelve precios/cupos, separa entradas generales y no altera inventario.
- Swagger documenta Bearer en CRUD, acceso anónimo en mapa, ejemplos de creación/edición y respuestas de error.
- Las pruebas de arquitectura verifican las dependencias de capas y controllers sin DbContext ni adaptadores.

## Revisión contra AGENTS.md §§5 y 12

No se identificaron incumplimientos en este bloque. No se modifican migraciones ni esquema bi, no hay paquetes nuevos, credenciales versionadas, DELETE físicos, lógica de negocio en controllers ni dependencias hacia Infrastructure en Domain/Application. Fechas de creación usan IClock/UTC y las operaciones async reciben CancellationToken. Auditoría y cambios se confirman juntos; los reintentos 40001/40P01 repiten toda la transacción.

Reserva atómica de boletos, ledger, QR, consumidores/outbox de compras, reembolsos y reseñas no cambian en este bloque: sus casos de uso corresponden a fases posteriores.

## Límites y pendientes

- La consulta del mapa muestra inventario leído y no constituye una reserva. La ventana de venta y la compra se validarán en F5.
- Sin eventoId, cupoDisponible=null; sin tipos activos de una zona para el evento indicado, cupoDisponible=0.
- Se publican mapas con evento solo para PROGRAMADO/EN_VENTA/AGOTADO. No se implementan endpoints nuevos de catálogo en este PR.
- No se comprueba intersección de aristas ni dimensiones de la imagen del polígono; se valida formato, puntos distintos y coordenadas finitas.
- imagenPlanoUrl es un dato; el servicio de subida/almacenamiento de imágenes se implementará en F4-09.
- El alta de recinto como administrador exige duenoId del perfil, distinto de usuarioId; no se añade un endpoint de búsqueda de perfiles en este bloque.
- La prueba manual de mapa con inventario requiere eventos/tipos existentes. Las pruebas automatizadas los crean en la base temporal; las altas HTTP llegarán en F4-05/F4-07.
- GitHub Actions debe confirmar el CI al publicar el PR. Guía local: [RECINTOS_LOCALIDADES_MAPA.md](RECINTOS_LOCALIDADES_MAPA.md).
