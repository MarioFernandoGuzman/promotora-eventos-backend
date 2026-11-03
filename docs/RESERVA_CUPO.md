# Reserva y devolución de cupo — F5-02

Application define `ICupoEntradaRepository`; Infrastructure lo implementa con PostgreSQL mediante `EfCupoEntradaRepository`. Ambas operaciones exigen una transacción explícita del caso de uso. No hay cambios de esquema ni nuevos endpoints en esta tarea.

La reserva descuenta con `UPDATE ... WHERE id = ... AND evento_id = ... AND activo AND cupo_disponible >= cantidad`. Una fila afectada significa reserva ganada; cero significa que no pudo reservarse. Nunca se lee el cupo para decidir el descuento en C#. Al reservar varios tipos, el llamador debe revertir toda la transacción si alguno falla.

La devolución cambia condicionalmente `PENDIENTE → CANCELADA/EXPIRADA` o `PROCESANDO → FALLIDA`. Expirar exige además `expira_en <= ahora`. Solamente quien gana el cambio suma las cantidades de los detalles, agrupadas y ordenadas por tipo de entrada. Cambiar estado y devolver cupo forman una sola transacción; un rollback revierte ambos. Esta operación no implementa reembolsos ni es un endpoint de cancelación: esos casos corresponden a F7 y F5-06. La autorización y auditoría corresponden al handler que la invoque.

Pruebas con PostgreSQL + pgvector y migraciones Flyway en Testcontainers: 20 reservas sobre 5 lugares producen exactamente 5 ganadores; 20 cancelaciones/expiraciones de una compra devuelven cupo exactamente una vez; detalles repetidos se suman; rollback conserva estado/cupo originales; estados incompatibles, expiración prematura, cantidades inválidas y operaciones fuera de transacción se rechazan.

Con Docker operativo, desde la raíz:

```powershell
dotnet build -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests
```

```powershell
dotnet format --verify-no-changes
```

Los contenedores de prueba son temporales y no usan ni modifican la base local de Docker Compose.
