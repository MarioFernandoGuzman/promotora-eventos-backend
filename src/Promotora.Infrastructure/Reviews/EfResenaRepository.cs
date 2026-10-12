using Microsoft.EntityFrameworkCore;
using Promotora.Application.Reviews;
using Promotora.Domain.Reviews;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Reviews;

public sealed class EfResenaRepository(PromotoraDbContext db) : IResenaRepository
{
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
