using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Shared.Messaging;

public sealed class EfOutboxRelay(PromotoraDbContext db, IMessagePublisher publisher, IClock clock) : IOutboxRelay
{
    public async Task<ResultadoRelayOutbox> PublicarLoteAsync(int tamanoLote, CancellationToken ct)
    {
        if (tamanoLote is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(tamanoLote));
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("El relay necesita un contexto independiente de la transacción de negocio.");
        var publicados = 0;
        for (var i = 0; i < tamanoLote; i++)
        {
            // Una transacción por mensaje evita revertir confirmados anteriores si falla otro envío.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var rows = await db.OutboxMensajes.FromSqlRaw(
                "SELECT * FROM public.outbox_mensajes WHERE publicado = false ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED")
                .AsNoTracking().ToListAsync(ct);
            if (rows.Count == 0) break;
            var row = rows[0];
            using var payload = row.Payload;
            var confirmado = false;
            try
            {
                // Los asistentes permanecen en PostgreSQL; ni RabbitMQ ni su DLQ reciben datos personales.
                var body = row.Tipo == TiposMensaje.ProcesarCompra
                    ? JsonSerializer.SerializeToUtf8Bytes(new { publicId = payload.RootElement.GetProperty("publicId") })
                    : Encoding.UTF8.GetBytes(payload.RootElement.GetRawText());
                await publisher.PublicarAsync(new(row.Id, row.Tipo, body), ct);
                confirmado = true;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // No registrar excepción/payload: pueden contener configuración sensible.
                // Un timeout puede haber entregado: repetir SIEMPRE con el mismo MessageId.
            }
            ct.ThrowIfCancellationRequested();
            var fecha = clock.UtcNow;
            var filas = await db.OutboxMensajes.Where(m => m.Id == row.Id && !m.Publicado)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Intentos, m => m.Intentos + 1)
                    .SetProperty(m => m.Publicado, confirmado)
                    .SetProperty(m => m.FechaPublicado, confirmado ? fecha : (DateTime?)null), ct);
            if (filas != 1) throw new InvalidOperationException("El relay perdió la fila bloqueada.");
            await tx.CommitAsync(ct);
            if (!confirmado) return new(publicados, 1); // Evita un bucle caliente sobre el mismo fallo.
            publicados++;
        }
        return new(publicados, 0);
    }
}
