using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Domain.Catalog;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfTipoEntradaRepository(PromotoraDbContext db) : ITipoEntradaRepository
{
    public async Task<TipoEntrada?> FindAsync(int eventoId, int id, bool bloquear, CancellationToken ct)
    {
        if (bloquear) await db.TiposEntrada.FromSqlInterpolated($"SELECT * FROM tipos_entrada WHERE evento_id={eventoId} AND id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var row = await db.TiposEntrada.AsNoTracking().SingleOrDefaultAsync(x => x.EventoId == eventoId && x.Id == id, ct);
        return row is null ? null : ToDomain(row);
    }
    public async Task<TipoEntrada> InsertAsync(TipoEntrada tipo, CancellationToken ct)
    {
        var row = new TipoEntradaRow
        {
            EventoId = tipo.EventoId,
            LocalidadId = tipo.LocalidadId,
            Nombre = tipo.Datos.Nombre,
            Precio = tipo.Datos.Precio,
            CupoTotal = tipo.Datos.CupoTotal,
            CupoDisponible = tipo.CupoDisponible,
            Activo = true
        };
        db.TiposEntrada.Add(row); await db.SaveChangesAsync(ct); return ToDomain(row);
    }
    public async Task<bool> UpdateAsync(TipoEntrada tipo, int cupoAnterior, CancellationToken ct)
    {
        int delta = tipo.Datos.CupoTotal - cupoAnterior;
        return await db.TiposEntrada.Where(x => x.Id == tipo.Id && x.EventoId == tipo.EventoId && x.Activo && x.CupoTotal == cupoAnterior && x.CupoDisponible == cupoAnterior)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Nombre, tipo.Datos.Nombre).SetProperty(x => x.Precio, tipo.Datos.Precio)
                .SetProperty(x => x.CupoTotal, tipo.Datos.CupoTotal).SetProperty(x => x.CupoDisponible, x => x.CupoDisponible + delta), ct) == 1;
    }
    public async Task<bool> DeactivateAsync(int eventoId, int id, CancellationToken ct) => await db.TiposEntrada.Where(x => x.EventoId == eventoId && x.Id == id && x.Activo)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, false), ct) == 1;
    public Task<long> OtrosCuposAsync(int eventoId, int localidadId, int? excluirId, CancellationToken ct) => db.TiposEntrada
        .Where(x => x.EventoId == eventoId && x.LocalidadId == localidadId && (excluirId == null || x.Id != excluirId)).SumAsync(x => (long)x.CupoTotal, ct);
    public async Task<PaginaDto<TipoEntradaDto>> ListAsync(int eventoId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.TiposEntrada.AsNoTracking().Where(x => x.EventoId == eventoId);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => TipoEntradaDto.From(ToDomain(x))).ToArray(), page, pageSize, total);
    }
    internal static TipoEntrada ToDomain(TipoEntradaRow r) => new(r.Id, r.EventoId, r.LocalidadId, new DatosTipoEntrada(r.Nombre, r.Precio, r.CupoTotal), r.CupoDisponible, r.Activo);
}
