using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Domain.Catalog;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfPresentadorRepository(PromotoraDbContext db) : IPresentadorRepository
{
    public async Task<Presentador?> FindAsync(int id, bool bloquear, CancellationToken ct)
    {
        if (bloquear)
            await db.Presentadores.FromSqlInterpolated($"SELECT * FROM presentadores WHERE id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var row = await db.Presentadores.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<Presentador> InsertAsync(Presentador entity, CancellationToken ct)
    {
        var row = new PresentadorRow { Nombre = entity.Datos.Nombre, Tipo = entity.Datos.Tipo, Biografia = entity.Datos.Biografia, ImagenUrl = entity.Datos.ImagenUrl, Activo = true };
        db.Presentadores.Add(row);
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task UpdateAsync(Presentador entity, CancellationToken ct) => await db.Presentadores.Where(x => x.Id == entity.Id && x.Activo)
        .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Nombre, entity.Datos.Nombre).SetProperty(x => x.Tipo, entity.Datos.Tipo).SetProperty(x => x.Biografia, entity.Datos.Biografia).SetProperty(x => x.ImagenUrl, entity.Datos.ImagenUrl), ct);

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct) => await db.Presentadores.Where(x => x.Id == id && x.Activo)
        .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Activo, false), ct) == 1;

    public async Task<PaginaDto<PresentadorDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        int total = await db.Presentadores.CountAsync(ct);
        var rows = await db.Presentadores.AsNoTracking().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => PresentadorDto.From(ToDomain(x))).ToArray(), page, pageSize, total);
    }

    private static Presentador ToDomain(PresentadorRow row) => new(row.Id, new DatosPresentador(row.Nombre, row.Tipo, row.Biografia, row.ImagenUrl), row.Activo);
}
