using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfCompraMensajeRepository(PromotoraDbContext db) : ICompraMensajeRepository
{
    public async Task<Result<IReadOnlyList<AsistentesDetalle>>> LeerAsync(MensajeCompraDto mensaje, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Leer mensaje de compra requiere transacción explícita.");
        var row = await db.OutboxMensajes.AsNoTracking().SingleOrDefaultAsync(o => o.Id == mensaje.MensajeId
            && o.Agregado == "Compra" && o.AgregadoId == mensaje.PublicId.ToString("D") && o.Tipo == TiposMensaje.ProcesarCompra, ct);
        if (row is null) return Fallo();
        using var payload = row.Payload;
        try
        {
            if (payload.RootElement.GetProperty("publicId").GetGuid() != mensaje.PublicId) return Fallo();
            var detalles = await (from d in db.DetalleCompra
                                  join c in db.Compras on d.CompraId equals c.Id
                                  where c.PublicId == mensaje.PublicId
                                  select new { d.Id, d.TipoEntradaId, d.Cantidad }).ToArrayAsync(ct);
            var asistentes = new List<AsistentesDetalle>();
            if (payload.RootElement.TryGetProperty("asistentes", out var lista))
            {
                foreach (var item in lista.EnumerateArray())
                {
                    int tipoId = item.GetProperty("tipoEntradaId").GetInt32();
                    var detalle = detalles.SingleOrDefault(d => d.TipoEntradaId == tipoId);
                    var personas = item.GetProperty("personas").Deserialize<DatosAsistente[]>();
                    if (detalle is null || personas is null || personas.Length != detalle.Cantidad || personas.Any(p => p is null)
                        || asistentes.Any(a => a.DetalleCompraId == detalle.Id)) return Fallo();
                    asistentes.Add(new(detalle.Id, personas));
                }
            }
            return Result<IReadOnlyList<AsistentesDetalle>>.Success(asistentes);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return Fallo();
        }
    }

    private static Result<IReadOnlyList<AsistentesDetalle>> Fallo() => Result<IReadOnlyList<AsistentesDetalle>>.Failure(
        new Error("compra.outbox", "El mensaje no corresponde a una solicitud de compra íntegra.", ErrorType.Validation));
}
