# Validación de salida — Fase 3

F3-01 a F3-07 implementadas en `feature/f3-identidad-seguridad`, desde develop con PR #10 mergeado.
No se modificó ninguna migración ni se agregó esquema. Siguen 26 tablas y 16 vistas BI.

## Evidencia local

Verificado en Windows, .NET 10 y Docker Desktop con PostgreSQL 16 + pgvector real:

```powershell
dotnet build --no-restore -warnaserror -c Release
dotnet format --verify-no-changes --no-restore
dotnet test --no-build -c Release --report-trx
pwsh -NoProfile -File scripts/Test-Compose.ps1 -WithApi
```

82 pruebas aprobadas, cero fallidas/omitidas: Domain 9, Application 4, Infrastructure 25,
API 32, Architecture 12. Testcontainers aplica los SQL Flyway existentes y usa login restringido.
El script Compose verificó 11 migraciones (V1–V10 y repeatable BI), migrate/validate/info,
RabbitMQ management y MailHog, readiness/liveness 200 y Swagger en Kestrel.
Además probó bootstrap, login inicial sin refresh, bloqueo de operaciones administrativas,
cambio obligatorio y consulta autorizada con la contraseña nueva. Solo retiró sus recursos temporales.

## Criterio de salida y AGENTS.md §§5/12

- Registro y altas crean usuario/perfil en una transacción, con auditoría; registro agrega outbox sin publicar.
- Bootstrap concurrente crea un único SUPERADMIN, conserva uno existente y no guarda credenciales en Git.
- Contraseñas con PasswordHasher/PBKDF2-SHA512 y sal; refresh con SHA-256, rotación y revocación familiar.
- La reutilización concurrente se confirma incluso al devolver 401; JWT de sesión revocada deja de servir.
- Roles, estado y propiedad se verifican en Application además de policies HTTP; las bajas/cambios toman locks.
- La transición a INACTIVO es condicional. No hay DELETE físico sobre usuarios/perfiles, compras o finanzas.
- Cambio de contraseña propio exige la actual, invalida sesiones y termina la obligación de cambio inicial.
- Auditoría solo INSERT con detalle acotado; el rollback incluye auditoría guardada previamente.
- Swagger tiene 11 operaciones, ejemplos, permisos Bearer y errores documentados; JSON camelCase y rutas v1.
- Controllers sin adaptadores/DbContext; Domain sin dependencias; Worker no requiere actor HTTP.
- UTC con IClock y CancellationToken; sin secretos/binarios/migraciones modificadas ni paquetes prohibidos.
- No se implementaron compras, pagos ni consumidores de otras fases; las protecciones Flyway existentes siguen probadas.

La revisión no encontró incumplimientos funcionales pendientes de Fase 3. Q1/Q9 siguen pendientes en la matriz
porque incluyen tareas de fases posteriores. GitHub Actions deberá confirmar CI verde al publicar este PR.

## Límites documentados

- Rate limiting por IP y proceso: cinco login/diez registros por minuto; un despliegue con réplicas requiere límite compartido.
- CORS no fija frontend ni permite orígenes externos sin configuración; Swagger funciona en el mismo origen.
- El bootstrap es inicialización, no recuperación de contraseña. No modifica una cuenta SUPERADMIN existente.
- Outbox de bienvenida queda pendiente de relay/consumidores en F6; MailHog está disponible pero F3 no envía correos.
- Las lecturas de perfiles/listados no generan acciones nuevas de auditoría: el catálogo SQL existente protege las mutaciones y autenticación.
