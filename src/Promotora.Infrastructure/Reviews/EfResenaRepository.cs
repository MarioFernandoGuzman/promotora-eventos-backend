using Microsoft.EntityFrameworkCore;
using Promotora.Application.Reviews;
using Promotora.Domain.Reviews;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Reviews;

public sealed class EfResenaRepository(PromotoraDbContext db) : IResenaRepository
{
    public async Task<CalificacionPresentadorDto?> CalificacionAsync(int presentadorId, CancellationToken ct)
    {
        if (!await db.Presentadores.AnyAsync(p => p.Id == presentadorId && p.Activo, ct)) return null;
        // Un único agregado asegura cantidad y suma del mismo snapshot; no promedia promedios por evento.
        var agregados = await (from r in db.Resenas
                               join ep in db.EventoPresentadores on r.EventoId equals ep.EventoId
                               where ep.PresentadorId == presentadorId && r.Estado == "APROBADA"
                               select r).GroupBy(r => 1).Select(g => new { Cantidad = g.LongCount(), Suma = g.Sum(r => (long)r.Calificacion) }).ToArrayAsync(ct);
        var agregado = agregados.SingleOrDefault(); long cantidad = agregado?.Cantidad ?? 0, suma = agregado?.Suma ?? 0;
        return new(presentadorId, CalificacionMedia.Calcular(suma, cantidad), cantidad);
    }
    public async Task<ResenaParaGestion?> LockAsync(int id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La gestión requiere transacción.");
        var rows = await db.Resenas.FromSqlInterpolated($"SELECT * FROM resenas WHERE id={id} FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
        var r = rows.SingleOrDefault();
        return r is null ? null : new(r.ClienteId, new(r.Id, r.EventoId, r.Calificacion, r.Comentario, r.Estado, r.FechaCreacion));
    }
    public Task<bool> CambiarEstadoAsync(int id, string esperado, string nuevo, CancellationToken ct) => CambiarAsync(id, esperado, nuevo, ct);
    private async Task<bool> CambiarAsync(int id, string esperado, string nuevo, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Cambiar estado requiere transacción.");
        return await db.Resenas.Where(r => r.Id == id && r.Estado == esperado).ExecuteUpdateAsync(s => s.SetProperty(r => r.Estado, nuevo), ct) == 1;
    }
    public async Task<Promotora.Application.Shared.PaginaDto<ResenaDto>> AdministracionAsync(ResenasAdministracionQuery request, CancellationToken ct)
    {
        var query = db.Resenas.AsNoTracking().Where(r => r.Estado == request.Estado);
        if (request.EventoId is int evento) query = query.Where(r => r.EventoId == evento);
        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.FechaCreacion).ThenByDescending(r => r.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(r => new ResenaDto(r.Id, r.EventoId, r.Calificacion, r.Comentario, r.Estado, r.FechaCreacion)).ToArrayAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
    public Task<int?> ClienteIdAsync(int usuarioId, CancellationToken ct) => db.Clientes.Where(c => c.UsuarioId == usuarioId).Select(c => (int?)c.Id).SingleOrDefaultAsync(ct);
    public Task<bool> EventoExisteAsync(int eventoId, CancellationToken ct) => db.Eventos.AnyAsync(e => e.Id == eventoId, ct);
    public Task<bool> AsistioAsync(int clienteId, int eventoId, CancellationToken ct) =>
        (from b in db.Boletos
         join d in db.DetalleCompra on b.DetalleCompraId equals d.Id
         join c in db.Compras on d.CompraId equals c.Id
         where c.ClienteId == clienteId && c.EventoId == eventoId && b.Estado == "USADO"
         select b.Id).AnyAsync(ct);
    public async Task<ResenaDto?> CrearAsync(int clienteId, int eventoId, ContenidoResena contenido, DateTime fecha, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || fecha.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Crear reseña requiere transacción y fecha UTC.");
        int filas = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO resenas (evento_id, cliente_id, calificacion, comentario, estado, fecha_creacion) VALUES ({eventoId}, {clienteId}, {contenido.Calificacion}, {contenido.Comentario}, 'PENDIENTE', {fecha}) ON CONFLICT (evento_id, cliente_id) DO NOTHING", ct);
        if (filas == 0) return null;
        return await db.Resenas.AsNoTracking().Where(r => r.EventoId == eventoId && r.ClienteId == clienteId)
            .Select(r => new ResenaDto(r.Id, r.EventoId, r.Calificacion, r.Comentario, r.Estado, r.FechaCreacion)).SingleAsync(ct);
    }
}
