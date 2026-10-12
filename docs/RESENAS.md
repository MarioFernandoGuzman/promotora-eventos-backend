# Reseñas — Fase 8

Contrato aprobado: calificación entera 1–5 y comentario opcional de máximo **510 caracteres**. Se valida el texto recibido antes de recortar espacios exteriores; un comentario vacío se guarda como null. El límite de C#/FluentValidation cuenta unidades UTF-16, igual que maxlength en un input del navegador.

## Crear — F8-01

POST /api/v1/resenas, solo CLIENTE activo con contraseña inicial cambiada. El cliente se obtiene de la sesión: no se acepta clienteId enviado por el frontend. Requiere un boleto USADO del evento, no basta comprar ni tener un boleto VALIDO. Se crea PENDIENTE y aún no se publica. Una por evento/cliente, incluso si luego es rechazada o retirada; para reenviar se editará la existente.

```json
{"eventoId":1,"calificacion":5,"comentario":"Excelente evento"}
```

Respuesta 200 con id, eventoId, calificacion, comentario, estado y fechaCreacion UTC. 400 para contenido/ID inválido, 401/403 para sesión/rol no habilitado, 404 evento inexistente, 409 duplicado y 422 sin boleto USADO. La restricción UNIQUE y INSERT ON CONFLICT evitan duplicados concurrentes. La creación y auditoría CREATE comparten transacción; no se guarda comentario en logs de auditoría.

Domain/Reviews valida el contenido; Application coordina validación, sesión, asistencia, puertos y auditoría; Infrastructure implementa EF/SQL; WebApi publica el contrato. Sin nuevos paquetes ni cambios de esquema. fechaCreacion se lee persistida después de insertar, conservando precisión de PostgreSQL.

Verificación F8-01: 428 pruebas Release aprobadas, cero fallidas/omitidas; build sin warnings y formato limpio. Swagger tiene 66 operaciones en este bloque.

## Prueba local

### Moderación — F8-02

GET /api/v1/resenas es la cola administrativa, PENDIENTE por defecto; admite estado PENDIENTE/APROBADA/RECHAZADA y eventoId opcional. page 1–100000, pageSize 1–100, default 20; orden fechaCreacion/ID descendentes. Solo ADMINISTRADOR habilitado. No expone clienteId, nombre, correo ni tokens.

PATCH /api/v1/resenas/{id}/moderar recibe `{"estadoNuevo":"APROBADA"}` o RECHAZADA. Solo PENDIENTE puede cambiar; misma decisión repetida devuelve 200 sin otra auditoría. Dos administradores con decisiones opuestas: uno gana y el otro recibe 409. ID inexistente 404, body inválido 400, sesión/rol inválido 401/403. Transición condicional y MODERATE en la misma transacción. Como administrador, consultar la cola, aprobar/rechazar y repetir; la fila desaparece del filtro PENDIENTE.

Verificación F8-02: 436 pruebas Release aprobadas, cero fallidas/omitidas, build sin warnings y formato limpio. Swagger: 68 operaciones.

Usar credenciales y secretos existentes; autenticación en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). Infraestructura y API según [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md); venta completa en [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md).

```powershell
dotnet run --project src/Promotora.WebApi
```

```powershell
dotnet run --project src/Promotora.Worker
```

1. Comprar y esperar COMPLETADA como CLIENTE. Crear reseña antes del escaneo debe dar 422.
2. Como ADMINISTRADOR o dueño autorizado, validar un QR mediante POST /api/v1/boletos/validar.
3. Volver a Authorize como el CLIENTE propietario y crear reseña para ese evento: 200 PENDIENTE.
4. Repetir: 409 sin otra fila ni auditoría. Comentario de 510 caracteres válido; 511 devuelve 400. Admin/dueño no pueden crear reseñas como clientes.

## Comprobaciones automáticas

Docker operativo; los tests usan PostgreSQL temporal sin modificar tu base local.

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```
