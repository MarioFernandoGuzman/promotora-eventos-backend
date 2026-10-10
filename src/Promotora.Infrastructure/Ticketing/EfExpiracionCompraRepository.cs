using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfExpiracionCompraRepository(PromotoraDbContext db) : IExpiracionCompraRepository
{
    public async Task<IReadOnlyList<int>> VencidasAsync(DateTime ahora, int lote, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La expiración requiere transacción explícita.");
        if (ahora.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(ahora));
        if (lote is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(lote));
        return await db.Compras.AsNoTracking().Where(c => c.Estado == "PENDIENTE" && c.ExpiraEn <= ahora && c.FechaCompra <= ahora)
            .OrderBy(c => c.ExpiraEn).ThenBy(c => c.Id).Select(c => c.Id).Take(lote).ToArrayAsync(ct);
    }
}
