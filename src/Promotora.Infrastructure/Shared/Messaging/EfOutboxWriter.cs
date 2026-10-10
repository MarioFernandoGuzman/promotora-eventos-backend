using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Shared.Messaging;

public sealed class EfOutboxWriter(PromotoraDbContext db, IClock clock) : IOutboxWriter
{
    public async Task<long> EscribirAsync(NuevoMensajeOutbox mensaje, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Outbox requiere la transacción de negocio activa.");
        if (string.IsNullOrWhiteSpace(mensaje.Agregado) || string.IsNullOrWhiteSpace(mensaje.AgregadoId)
            || !TiposMensaje.EsValido(mensaje.Tipo) || mensaje.Payload.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Outbox requiere agregado, identidad, tipo admitido y objeto JSON.", nameof(mensaje));
        using var payload = JsonDocument.Parse(mensaje.Payload.GetRawText());
        var row = new OutboxMensajeRow
        {
            Agregado = mensaje.Agregado,
            AgregadoId = mensaje.AgregadoId,
            Tipo = mensaje.Tipo,
            Payload = payload,
            Fecha = clock.UtcNow
        };
        db.OutboxMensajes.Add(row);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(row).State = EntityState.Detached; }
        return row.Id;
    }
}
