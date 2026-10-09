using System.Text.Json;
using System.Text.Json.Serialization;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Audit.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Audit;

public sealed class EfAuditLogger(PromotoraDbContext db, IClock clock) : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<MotivoAuditoria>() }
    };

    public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct)
        => WriteAsync(usuarioId, accion, entidad, entidadId, ip, null, ct);

    public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        db.LogsAuditoria.Add(new LogAuditoriaRow
        {
            UsuarioId = usuarioId,
            Accion = accion,
            EntidadAfectada = entidad,
            EntidadId = entidadId,
            IpOrigen = ip,
            FechaHora = clock.UtcNow,
            Detalle = detalle is null ? null : JsonSerializer.SerializeToDocument(detalle, JsonOptions)
        });
        // La unidad de trabajo confirma junto al cambio de negocio.
        return Task.CompletedTask;
    }
}
