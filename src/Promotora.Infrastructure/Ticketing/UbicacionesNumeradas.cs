using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

internal static class UbicacionesNumeradas
{
    internal static async Task<Dictionary<long, UbicacionBoletoDto>> LeerAsync(PromotoraDbContext db, long[] boletoIds, CancellationToken ct)
    {
        var filas = await (from s in db.AsignacionesAsiento.AsNoTracking()
                           join a in db.AsientosEvento on s.AsientoId equals a.Id
                           join d in db.DistribucionesEvento on a.DistribucionId equals d.Id
                           join m in db.MesasEvento on a.MesaId equals m.Id into mesas
                           from m in mesas.DefaultIfEmpty()
                           where s.BoletoId != null && boletoIds.Contains(s.BoletoId!.Value)
                           select new { BoletoId = s.BoletoId!.Value, Posicion = new UbicacionBoletoDto(a.Id, a.Etiqueta, a.Fila, a.MesaId, m == null ? null : m.Etiqueta, d.LocalidadId) }).ToArrayAsync(ct);
        return filas.ToDictionary(f => f.BoletoId, f => f.Posicion);
    }

    internal static async Task LiberarAsync(PromotoraDbContext db, int compraId, long[]? boletosAnulados, DateTime ahora, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || ahora.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Liberación requiere transacción y UTC.");
        string esperado = boletosAnulados is null ? "RESERVADA" : "CONFIRMADA";
        var asignaciones = await db.AsignacionesAsiento.AsNoTracking().Where(s => s.Estado == esperado &&
            db.DetalleCompra.Any(d => d.Id == s.DetalleCompraId && d.CompraId == compraId) &&
            (boletosAnulados == null || s.BoletoId != null && boletosAnulados.Contains(s.BoletoId.Value)))
            .OrderBy(s => s.AsientoId).ToArrayAsync(ct);
        foreach (var s in asignaciones)
        {
            int filas = await db.AsignacionesAsiento.Where(a => a.Id == s.Id && a.Estado == esperado && a.DetalleCompraId == s.DetalleCompraId)
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.Estado, "LIBERADA").SetProperty(a => a.FechaLiberacion, ahora), ct);
            if (filas != 1) throw new InvalidOperationException("La reserva no pertenece a la operación que libera.");
            filas = await db.AsientosEvento.Where(a => a.Id == s.AsientoId && a.Estado == (boletosAnulados == null ? "RESERVADO" : "VENDIDO"))
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.Estado, "DISPONIBLE"), ct);
            if (filas != 1) throw new InvalidOperationException("La posición no corresponde a su asignación.");
        }
    }
}
