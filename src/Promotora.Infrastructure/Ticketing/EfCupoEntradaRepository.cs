using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfCupoEntradaRepository(PromotoraDbContext db) : ICupoEntradaRepository
{
    public async Task<bool> ReservarAsync(int eventoId, int tipoEntradaId, int cantidad, CancellationToken ct)
    {
        ExigirTransaccion();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);
        var filas = await db.TiposEntrada
            .Where(t => t.Id == tipoEntradaId && t.EventoId == eventoId && t.Activo && t.CupoDisponible >= cantidad)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, t => t.CupoDisponible - cantidad), ct);
        return filas == 1;
    }

    public async Task<bool> FinalizarYDevolverAsync(int compraId, EstadoCompra esperado, EstadoCompra siguiente, DateTime ahora, CancellationToken ct)
    {
        ExigirTransaccion();
        if (ahora.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahora));
        }

        var permitida = esperado == EstadoCompra.PENDIENTE && siguiente is EstadoCompra.CANCELADA or EstadoCompra.EXPIRADA
            || esperado == EstadoCompra.PROCESANDO && siguiente == EstadoCompra.FALLIDA;
        if (!permitida)
        {
            throw new ArgumentException("La transición no permite devolver cupo.", nameof(siguiente));
        }

        var estadoEsperado = esperado.ToString();
        var estadoSiguiente = siguiente.ToString();
        var compras = db.Compras.Where(c => c.Id == compraId && c.Estado == estadoEsperado && c.FechaCompra <= ahora);
        if (siguiente == EstadoCompra.EXPIRADA)
        {
            compras = compras.Where(c => c.ExpiraEn <= ahora);
        }

        var filas = await compras.ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, estadoSiguiente), ct);
        if (filas == 0)
        {
            return false;
        }

        // El bloqueo de la compra dura hasta el COMMIT: solamente el ganador devuelve cupo.
        var detalles = await db.DetalleCompra.Where(d => d.CompraId == compraId)
            .GroupBy(d => d.TipoEntradaId)
            .Select(g => new { TipoEntradaId = g.Key, Cantidad = g.Sum(d => d.Cantidad) })
            .OrderBy(d => d.TipoEntradaId).ToListAsync(ct);
        foreach (var detalle in detalles)
        {
            await db.TiposEntrada.Where(t => t.Id == detalle.TipoEntradaId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, t => t.CupoDisponible + detalle.Cantidad), ct);
        }

        await UbicacionesNumeradas.LiberarAsync(db, compraId, null, ahora, ct);

        return true;
    }

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("La reserva y devolución requieren una transacción explícita.");
        }
    }
}
