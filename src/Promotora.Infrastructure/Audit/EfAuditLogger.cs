using Promotora.Application.Audit;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Audit.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Audit;

public sealed class EfAuditLogger(PromotoraDbContext db, IClock clock) : IAuditLogger
{
    public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        db.LogsAuditoria.Add(new LogAuditoriaRow
        {
            UsuarioId = usuarioId,
            Accion = accion,
            EntidadAfectada = entidad,
            EntidadId = entidadId,
            IpOrigen = ip,
            FechaHora = clock.UtcNow
        });
        // La unidad de trabajo confirma junto al cambio de negocio.
        return Task.CompletedTask;
    }
}
