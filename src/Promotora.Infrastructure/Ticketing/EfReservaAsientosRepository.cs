using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfReservaAsientosRepository(PromotoraDbContext db) : IReservaAsientosPort
{
    public async Task<Result<Unit>> ReservarAsync(int eventoId, IReadOnlyList<ItemCompra> items, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Reserva numerada requiere transacción.");
        int[] tipos = items.Select(i => i.TipoEntradaId).ToArray();
        var configuradas = await (from t in db.TiposEntrada
                                  join d in db.DistribucionesEvento
                                  on new { t.EventoId, LocalidadId = t.LocalidadId!.Value } equals new { d.EventoId, d.LocalidadId }
                                  where t.EventoId == eventoId && t.LocalidadId != null && tipos.Contains(t.Id)
                                  select new { TipoId = t.Id, DistribucionId = d.Id, d.Estado }).ToArrayAsync(ct);
        foreach (var item in items.OrderBy(i => i.TipoEntradaId))
        {
            var config = configuradas.SingleOrDefault(c => c.TipoId == item.TipoEntradaId);
            if (config is null || config.Estado == "BORRADOR")
            {
                if (item.AsientoIds is not null) return Fallo("numeracion.general", "La entrada no tiene distribución publicada.", ErrorType.BusinessRule);
                // Borrador no convierte todavía el tipo general; publicar requiere ausencia de historial.
                continue;
            }
            if (item.AsientoIds is null) return Fallo("numeracion.requerida", "Selecciona las sillas de la localidad numerada.", ErrorType.BusinessRule);
            long[] ids = item.AsientoIds.ToArray();
            await db.AsientosEvento.FromSqlInterpolated($"SELECT * FROM asientos_evento WHERE id=ANY({ids}) AND distribucion_id={config.DistribucionId} ORDER BY id FOR UPDATE").AsNoTracking().ToListAsync(ct);
            var filas = await db.AsientosEvento.AsNoTracking().Where(a => a.DistribucionId == config.DistribucionId && ids.Contains(a.Id)).ToArrayAsync(ct);
            long[] mesaIds = filas.Where(a => a.MesaId.HasValue).Select(a => a.MesaId!.Value).Distinct().ToArray();
            var mesas = await db.MesasEvento.AsNoTracking().Where(m => mesaIds.Contains(m.Id)).ToArrayAsync(ct);
            var sillasMesa = await db.AsientosEvento.AsNoTracking().Where(a => a.MesaId != null && mesaIds.Contains(a.MesaId.Value)).Select(a => new { a.Id, a.MesaId }).ToArrayAsync(ct);
            var valido = SeleccionNumerada.Validar(ids, item.TipoEntradaId, item.Cantidad,
                filas.Select(a => new PosicionNumerada(a.Id, a.TipoEntradaId, a.MesaId, a.Estado == "DISPONIBLE")).ToArray(),
                mesas.Select(m => new MesaNumerada(m.Id, Enum.Parse<ModalidadMesa>(m.Modalidad), sillasMesa.Where(a => a.MesaId == m.Id).Select(a => a.Id).ToArray())).ToArray());
            if (valido.IsFailure) return valido;
            int afectadas = await db.AsientosEvento.Where(a => ids.Contains(a.Id) && a.DistribucionId == config.DistribucionId && a.Estado == "DISPONIBLE")
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, "RESERVADO"), ct);
            if (afectadas != ids.Length) return Fallo("numeracion.ocupada", "Otra compra tomó una posición seleccionada.", ErrorType.Conflict);
        }
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task AsignarAsync(int compraId, IReadOnlyList<ItemCompra> items, DateTime ahora, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || ahora.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Asignación exige transacción y UTC.");
        var detalles = await db.DetalleCompra.AsNoTracking().Where(d => d.CompraId == compraId).ToArrayAsync(ct);
        foreach (var item in items.Where(i => i.AsientoIds is not null))
        {
            var detalle = detalles.Single(d => d.TipoEntradaId == item.TipoEntradaId);
            for (int i = 0; i < item.AsientoIds!.Count; i++)
                db.AsignacionesAsiento.Add(new AsignacionAsientoRow { AsientoId = item.AsientoIds[i], DetalleCompraId = detalle.Id, OrdinalBoleto = i + 1, FechaReserva = ahora });
        }
        await db.SaveChangesAsync(ct);
    }
    private static Result<Unit> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<Unit>.Failure(new Error(codigo, mensaje, tipo));
}
