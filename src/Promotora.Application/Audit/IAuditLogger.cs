namespace Promotora.Application.Audit;

public interface IAuditLogger
{
    Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct);
}
