using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Finance;

public sealed class EfCuentaConsultaRepository(PromotoraDbContext db) : ICuentaConsultaRepository
{
    private IQueryable<CuentaFinancieraRow> Permitidas(int? dueno) => db.CuentasFinancieras.AsNoTracking()
        .Where(c => dueno == null || (c.Tipo == "DUENO" && c.DuenoId == dueno));
    public async Task<CuentaDto?> ObtenerAsync(int id, int? duenoPermitido, CancellationToken ct) =>
        await Permitidas(duenoPermitido).Where(c => c.Id == id)
            .Select(c => new CuentaDto(c.Id, c.Tipo, c.DuenoId, c.Moneda, c.Saldo, c.Estado, c.FechaCierre)).SingleOrDefaultAsync(ct);
    public async Task<PaginaDto<CuentaDto>> ListarAsync(CuentasQuery request, int? duenoPermitido, CancellationToken ct)
    {
        var query = Permitidas(duenoPermitido);
        if (request.DuenoId is int dueno) query = query.Where(c => c.DuenoId == dueno);
        if (request.Moneda is string moneda) query = query.Where(c => c.Moneda == moneda);
        if (request.Tipo is string tipo) query = query.Where(c => c.Tipo == tipo);
        if (request.Estado is string estado) query = query.Where(c => c.Estado == estado);
        int total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(c => new CuentaDto(c.Id, c.Tipo, c.DuenoId, c.Moneda, c.Saldo, c.Estado, c.FechaCierre)).ToArrayAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
    public async Task<PaginaDto<MovimientoCuentaDto>> MovimientosAsync(MovimientosCuentaQuery request, CancellationToken ct)
    {
        var query = db.MovimientosFinancieros.AsNoTracking().Where(m => m.CuentaId == request.Id);
        if (request.Tipo is string tipo) query = query.Where(m => m.Tipo == tipo);
        if (request.CompraId is int compra) query = query.Where(m => m.CompraId == compra);
        if (request.Desde is { } desde) query = query.Where(m => m.Fecha >= desde.UtcDateTime);
        if (request.Hasta is { } hasta) query = query.Where(m => m.Fecha < hasta.UtcDateTime);
        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(m => new MovimientoCuentaDto(m.Id, m.CuentaId, m.CompraId, m.Tipo, m.Monto, m.Fecha)).ToArrayAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
}
