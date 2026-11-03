using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfCompraConsultaRepository(PromotoraDbContext db) : ICompraConsultaRepository
{
    public async Task<CompraPropia?> ObtenerAsync(Guid publicId, int clienteId, CancellationToken ct)
    {
        var row = await db.Compras.AsNoTracking().SingleOrDefaultAsync(c => c.PublicId == publicId && c.ClienteId == clienteId, ct);
        if (row is null) return null;
        var detalles = await db.DetalleCompra.AsNoTracking().Where(d => d.CompraId == row.Id).OrderBy(d => d.Id).ToArrayAsync(ct);
        return new(row.Id, ToDto(row, detalles, await AsignacionesAsync(detalles, ct)));
    }

    public async Task<PaginaDto<CompraCreadaDto>> MisComprasAsync(int clienteId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Compras.AsNoTracking().Where(c => c.ClienteId == clienteId);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.FechaCompra).ThenByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        int[] ids = rows.Select(c => c.Id).ToArray();
        var detalles = await db.DetalleCompra.AsNoTracking().Where(d => ids.Contains(d.CompraId)).OrderBy(d => d.Id).ToArrayAsync(ct);
        var grupos = detalles.ToLookup(d => d.CompraId);
        var asignaciones = await AsignacionesAsync(detalles, ct);
        return new(rows.Select(c => ToDto(c, grupos[c.Id], asignaciones)).ToArray(), page, pageSize, total);
    }

    public async Task<PaginaDto<BoletoClienteDto>> MisBoletosAsync(int clienteId, int page, int pageSize, CancellationToken ct)
    {
        var query = from boleto in db.Boletos.AsNoTracking()
                    join detalle in db.DetalleCompra on boleto.DetalleCompraId equals detalle.Id
                    join compra in db.Compras on detalle.CompraId equals compra.Id
                    where compra.ClienteId == clienteId
                    select new { Boleto = boleto, CompraPublicId = compra.PublicId, compra.EventoId, detalle.TipoEntradaId };
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Boleto.FechaEmision).ThenByDescending(x => x.Boleto.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var ubicaciones = await UbicacionesNumeradas.LeerAsync(db, rows.Select(r => r.Boleto.Id).ToArray(), ct);
        return new(rows.Select(x => new BoletoClienteDto(x.Boleto.PublicId, x.CompraPublicId, x.EventoId, x.TipoEntradaId,
            x.Boleto.CodigoQr, x.Boleto.Estado, new(x.Boleto.NombreAsistente, x.Boleto.DocumentoIdentidad, x.Boleto.DatosExtra?.RootElement.GetRawText()),
            x.Boleto.FechaEmision, x.Boleto.FechaUso, ubicaciones.GetValueOrDefault(x.Boleto.Id))).ToArray(), page, pageSize, total);
    }

    private async Task<AsignacionAsientoRow[]> AsignacionesAsync(IEnumerable<DetalleCompraRow> detalles, CancellationToken ct)
    {
        int[] ids = detalles.Select(d => d.Id).ToArray();
        return await db.AsignacionesAsiento.AsNoTracking().Where(a => ids.Contains(a.DetalleCompraId)).OrderBy(a => a.OrdinalBoleto).ToArrayAsync(ct);
    }
    private static CompraCreadaDto ToDto(CompraRow c, IEnumerable<DetalleCompraRow> detalles, IReadOnlyList<AsignacionAsientoRow> asignaciones) => new(c.PublicId, c.EventoId, c.Estado,
        c.FechaCompra, c.ExpiraEn, c.Moneda, c.Subtotal, c.ComisionPlataforma, c.Impuestos, c.Total,
        detalles.Select(d => new DetalleCompraDto(d.TipoEntradaId, d.Cantidad, d.PrecioUnitario, d.Subtotal, asignaciones.Any(a => a.DetalleCompraId == d.Id) ? asignaciones.Where(a => a.DetalleCompraId == d.Id).Select(a => a.AsientoId).ToArray() : null)).ToArray());
}
