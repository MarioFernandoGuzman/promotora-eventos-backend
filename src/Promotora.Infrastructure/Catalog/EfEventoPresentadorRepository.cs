using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfEventoPresentadorRepository(PromotoraDbContext db) : IEventoPresentadorRepository
{
    public Task<EventoPresentadorDto?> FindAsync(int eventoId, int presentadorId, CancellationToken ct) =>
        (from relation in db.EventoPresentadores.AsNoTracking()
         join p in db.Presentadores.AsNoTracking() on relation.PresentadorId equals p.Id
         where relation.EventoId == eventoId && relation.PresentadorId == presentadorId
         select new EventoPresentadorDto(eventoId, p.Id, p.Nombre, p.Tipo, relation.Rol, p.Activo)).SingleOrDefaultAsync(ct);
    public async Task InsertAsync(int eventoId, int presentadorId, string? rol, CancellationToken ct)
    {
        db.EventoPresentadores.Add(new EventoPresentadorRow { EventoId = eventoId, PresentadorId = presentadorId, Rol = rol });
        await db.SaveChangesAsync(ct);
    }
    public async Task UpdateRoleAsync(int eventoId, int presentadorId, string? rol, CancellationToken ct) =>
        await db.EventoPresentadores.Where(x => x.EventoId == eventoId && x.PresentadorId == presentadorId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Rol, rol), ct);
    public async Task<bool> DeleteAsync(int eventoId, int presentadorId, CancellationToken ct) =>
        await db.EventoPresentadores.Where(x => x.EventoId == eventoId && x.PresentadorId == presentadorId).ExecuteDeleteAsync(ct) == 1;
    public async Task<PaginaDto<EventoPresentadorDto>> ListAsync(int eventoId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.EventoPresentadores.AsNoTracking().Where(x => x.EventoId == eventoId);
        int total = await query.CountAsync(ct);
        var rows = query.OrderBy(x => x.PresentadorId).Skip((page - 1) * pageSize).Take(pageSize);
        var items = await (from relation in rows
                           join p in db.Presentadores.AsNoTracking() on relation.PresentadorId equals p.Id
                           orderby relation.PresentadorId
                           select new EventoPresentadorDto(eventoId, p.Id, p.Nombre, p.Tipo, relation.Rol, p.Activo)).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }
}
