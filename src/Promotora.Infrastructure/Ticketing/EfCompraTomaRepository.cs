using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfCompraTomaRepository(PromotoraDbContext db, IClock clock, IAuditLogger audit) : ICompraTomaRepository
{
    public async Task<CompraTomadaDto?> TomarAsync(Guid publicId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Tomar una compra requiere transacción explícita.");
        if (publicId == Guid.Empty) throw new ArgumentException("Referencia de compra inválida.", nameof(publicId));
        var ahora = clock.UtcNow;
        int filas = await db.Compras.Where(c => c.PublicId == publicId && c.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Estado, "PROCESANDO")
                .SetProperty(c => c.ProcesandoDesde, ahora), ct);
        if (filas == 0) return null;
        var compra = await db.Compras.AsNoTracking().Where(c => c.PublicId == publicId)
            .Select(c => new CompraTomadaDto(c.Id, c.PublicId, c.Subtotal, c.ComisionPlataforma, c.Impuestos, c.Total, c.Moneda)).SingleAsync(ct);
        await audit.WriteAsync(null, "UPDATE", "compras", compra.Id.ToString(CultureInfo.InvariantCulture), null, ct);
        return compra;
    }
}
