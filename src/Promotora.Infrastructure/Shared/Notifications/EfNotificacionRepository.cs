using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Notifications;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Shared.Notifications;

public sealed class EfNotificacionRepository(PromotoraDbContext db, IEmisionBoletosRepository emision, IClock clock) : INotificacionRepository
{
    public async Task<ContenidoNotificacion?> CompraAsync(Guid publicId, CancellationToken ct)
    {
        ExigirTransaccion();
        var compra = await emision.LockCompraAsync(publicId, ct);
        if (compra is null || compra.Estado != "COMPLETADA" || compra.Detalles.Count == 0) return null;
        var respaldo = await emision.RespaldoAsync(compra, ct);
        if (!respaldo.PermiteEmision(compra.Importes)) return null;
        var boletos = await emision.ListarAsync(compra.Id, ct);
        if (boletos.Count != compra.Detalles.Sum(d => (long)d.Cantidad)
            || compra.Detalles.Any(d => boletos.Count(b => b.DetalleCompraId == d.Id) != d.Cantidad)
            || boletos.Any(b => b.Estado is not ("VALIDO" or "USADO"))) return null;
        var destino = await (from c in db.Compras
                             join cliente in db.Clientes on c.ClienteId equals cliente.Id
                             join u in db.Usuarios on cliente.UsuarioId equals u.Id
                             where c.Id == compra.Id && u.Estado == "ACTIVO"
                             select new { u.Id, u.Email }).SingleOrDefaultAsync(ct);
        if (destino is null) return null;
        // Texto privado para SMTP: nunca persistir ni loguear códigos QR completos.
        var texto = $"Compra {publicId:D} confirmada. Presenta el código de tu boleto en puerta.\n"
            + string.Join("\n", boletos.Select(b => $"Boleto {b.PublicId:D}: {b.CodigoQr}" + (b.Ubicacion is null ? "" : $" — Localidad {b.Ubicacion.LocalidadId}, {b.Ubicacion.Mesa ?? "sin mesa"}, silla {b.Ubicacion.Silla}")));
        return new(destino.Id, destino.Email, "Confirmación de compra — Promotora", texto);
    }

    public Task RegistrarAsync(int? usuarioId, string tipo, string estado, string detalle, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ExigirTransaccion();
        db.Notificaciones.Add(new NotificacionRow
        {
            UsuarioId = usuarioId,
            Tipo = tipo,
            Estado = estado,
            Mensaje = detalle,
            FechaCreacion = clock.UtcNow,
            FechaEnvio = estado == "ENVIADO" ? clock.UtcNow : null
        });
        return Task.CompletedTask;
    }

    public async Task<ContenidoNotificacion?> UsuarioAsync(int usuarioId, CancellationToken ct)
    {
        ExigirTransaccion();
        var usuario = await db.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId && u.Estado == "ACTIVO" && u.Rol == "CLIENTE"
            && db.Clientes.Any(c => c.UsuarioId == u.Id)).Select(u => new { u.Id, u.Email }).SingleOrDefaultAsync(ct);
        return usuario is null ? null : new(usuario.Id, usuario.Email, "Bienvenido a Promotora",
            "Tu registro en Promotora está listo. Puedes iniciar sesión, consultar eventos y gestionar tus compras desde tu cuenta.");
    }

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Notificaciones requieren transacción explícita.");
    }
}
