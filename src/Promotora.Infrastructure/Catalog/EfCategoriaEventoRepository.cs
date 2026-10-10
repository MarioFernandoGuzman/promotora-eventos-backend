using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Domain.Catalog;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfCategoriaEventoRepository(PromotoraDbContext db) : ICategoriaEventoRepository
{
    public async Task<CategoriaEvento?> FindAsync(int id, bool bloquear, CancellationToken ct)
    {
        if (bloquear)
            await db.CategoriasEvento.FromSqlInterpolated($"SELECT * FROM categorias_evento WHERE id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var row = await db.CategoriasEvento.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<CategoriaEvento> InsertAsync(CategoriaEvento entity, CancellationToken ct)
    {
        var row = new CategoriaEventoRow { Nombre = entity.Datos.Nombre, Descripcion = entity.Datos.Descripcion, Activo = true };
        db.CategoriasEvento.Add(row);
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task UpdateAsync(CategoriaEvento entity, CancellationToken ct) => await db.CategoriasEvento.Where(x => x.Id == entity.Id && x.Activo)
        .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Nombre, entity.Datos.Nombre).SetProperty(x => x.Descripcion, entity.Datos.Descripcion), ct);

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct) => await db.CategoriasEvento.Where(x => x.Id == id && x.Activo)
        .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Activo, false), ct) == 1;

    public async Task<PaginaDto<CategoriaEventoDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        int total = await db.CategoriasEvento.CountAsync(ct);
        var rows = await db.CategoriasEvento.AsNoTracking().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => CategoriaEventoDto.From(ToDomain(x))).ToArray(), page, pageSize, total);
    }

    private static CategoriaEvento ToDomain(CategoriaEventoRow row) => new(row.Id, new DatosCategoriaEvento(row.Nombre, row.Descripcion), row.Activo);
}
