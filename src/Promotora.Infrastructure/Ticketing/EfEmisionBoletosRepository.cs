using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfEmisionBoletosRepository(PromotoraDbContext db, EmisionBoletosOptions options) : IEmisionBoletosRepository
{
    public async Task<CompraParaEmision?> LockCompraAsync(Guid publicId, CancellationToken ct)
    {
        ExigirTransaccion();
        var rows = await db.Compras.FromSqlInterpolated($"SELECT * FROM compras WHERE public_id={publicId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var c = rows.SingleOrDefault();
        if (c is null) return null;
        int categoria = await db.Eventos.Where(e => e.Id == c.EventoId).Select(e => e.CategoriaId).SingleAsync(ct);
        bool? esCarrera = options.CategoriaCarreraId is int categoriaCarrera && await db.CategoriasEvento.AnyAsync(cat => cat.Id == categoriaCarrera, ct)
            ? categoria == categoriaCarrera : null;
        var detalles = await db.DetalleCompra.Where(d => d.CompraId == c.Id).OrderBy(d => d.Id).Select(d => new DetalleParaEmision(d.Id, d.Cantidad)).ToArrayAsync(ct);
        var importes = ImportesCompra.DesdeMontos(c.Subtotal, c.ComisionPlataforma, c.Impuestos, c.Total);
        return new(c.Id, c.PublicId, c.Estado, c.Moneda, esCarrera, importes.Value, detalles);
    }

    public async Task<RespaldoCobro> RespaldoAsync(CompraParaEmision compra, CancellationToken ct)
    {
        ExigirTransaccion();
        int dueno = await (from c in db.Compras
                           join evento in db.Eventos on c.EventoId equals evento.Id
                           join recinto in db.Recintos on evento.RecintoId equals recinto.Id
                           where c.Id == compra.Id
                           select recinto.DuenoId).SingleAsync(ct);
        var filas = await (from m in db.MovimientosFinancieros
                           join cuenta in db.CuentasFinancieras on m.CuentaId equals cuenta.Id
                           where m.CompraId == compra.Id
                           select new { m.Tipo, m.Monto, TipoCuenta = cuenta.Tipo, cuenta.DuenoId, cuenta.Moneda }).ToArrayAsync(ct);
        int aprobados = await db.Pagos.CountAsync(p => p.CompraId == compra.Id && p.Estado == "APROBADO" && p.Monto == compra.Importes.Total
            && p.Referencia == compra.PublicId.ToString(), ct);
        bool cuentas = filas.All(f => f.Moneda == compra.Moneda && (f.Tipo switch
        {
            "VENTA" => f.TipoCuenta == "DUENO" && f.DuenoId == dueno,
            "COMISION" => f.TipoCuenta == "PLATAFORMA",
            "IMPUESTO" => f.TipoCuenta == "IMPUESTOS",
            _ => false
        }));
        var idsCuenta = filas.Length == 0 ? [] : await db.MovimientosFinancieros.Where(m => m.CompraId == compra.Id).Select(m => m.CuentaId).Distinct().ToArrayAsync(ct);
        bool desbalance = await db.CuentasFinancieras.Where(c => idsCuenta.Contains(c.Id)).AnyAsync(c => c.Saldo !=
            (db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), ct);
        return new(aprobados == 1, cuentas && !desbalance, filas.All(f => f.Monto > 0m), filas.LongLength,
            filas.Where(f => f.Tipo == "VENTA").Sum(f => f.Monto), filas.Where(f => f.Tipo == "COMISION").Sum(f => f.Monto), filas.Where(f => f.Tipo == "IMPUESTO").Sum(f => f.Monto));
    }

    public async Task<IReadOnlyList<BoletoEmitidoDto>> ListarAsync(int compraId, CancellationToken ct)
    {
        var rows = await db.Boletos.AsNoTracking().Where(b => db.DetalleCompra.Any(d => d.Id == b.DetalleCompraId && d.CompraId == compraId))
            .OrderBy(b => b.Id).ToArrayAsync(ct);
        return rows.Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<BoletoEmitidoDto>> InsertarAsync(IReadOnlyList<Boleto> boletos, CancellationToken ct)
    {
        ExigirTransaccion();
        var rows = boletos.Select(b => new BoletoRow
        {
            PublicId = b.PublicId,
            DetalleCompraId = b.DetalleCompraId,
            CodigoQr = b.CodigoQr,
            Estado = b.Estado,
            NombreAsistente = b.Asistente.NombreAsistente,
            DocumentoIdentidad = b.Asistente.DocumentoIdentidad,
            DatosExtra = b.Asistente.DatosExtraJson is null ? null : JsonDocument.Parse(b.Asistente.DatosExtraJson),
            FechaEmision = b.FechaEmision
        }).ToArray();
        db.Boletos.AddRange(rows); await db.SaveChangesAsync(ct);
        return rows.Select(ToDto).ToArray();
    }

    private static BoletoEmitidoDto ToDto(BoletoRow b) => new(b.Id, b.PublicId, b.DetalleCompraId, b.CodigoQr, b.Estado,
        new(b.NombreAsistente, b.DocumentoIdentidad, b.DatosExtra?.RootElement.GetRawText()), b.FechaEmision);
    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La emisión requiere una transacción explícita.");
    }
}
