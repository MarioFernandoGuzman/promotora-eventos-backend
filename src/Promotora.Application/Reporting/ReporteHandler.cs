using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reporting;

public sealed class ReporteAccess(IUsuarioActual actual, IUsuarioRepository usuarios)
{
    public static readonly Error Forbidden = new("reporte.prohibido", "Solo un administrador habilitado puede consultar o exportar reportes.", ErrorType.Forbidden);
    public async Task<bool> AutorizarAsync(CancellationToken ct) => actual.UsuarioId is int id &&
        (await usuarios.FindByIdAsync(id, ct))?.EsAdministradorHabilitado == true;
}

public sealed class ReporteHandler(IReporteRepository repository, ReporteAccess access) : IQueryHandler<ReporteQuery, ReporteDto>
{
    public async Task<Result<ReporteDto>> HandleAsync(ReporteQuery query, CancellationToken ct)
    {
        if (!await access.AutorizarAsync(ct)) return Result<ReporteDto>.Failure(ReporteAccess.Forbidden);
        return await repository.ConsultarAsync(query, ct);
    }
}
