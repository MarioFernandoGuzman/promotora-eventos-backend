using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reporting;

public sealed record ExportarReporteQuery(ReporteQuery Reporte, string Formato) : IQuery<ArchivoReporteDto>;
public sealed record ArchivoReporteDto(Stream Contenido, string ContentType, string Nombre);
public interface IReportExporter
{
    Task<ArchivoReporteDto> ExportarAsync(ReporteDto reporte, string formato, CancellationToken ct);
}

public sealed class ExportarReporteValidator : AbstractValidator<ExportarReporteQuery>
{
    public ExportarReporteValidator()
    {
        RuleFor(x => x.Formato).Must(x => x is "xlsx" or "pdf");
        RuleFor(x => x.Reporte).SetValidator(new ReporteValidator());
    }
}

public sealed class ExportarReporteHandler(IReporteRepository repository, ReporteAccess access,
    IReportExporter exporter, IAuditLogger audit, IUsuarioActual actual, IUnitOfWork unitOfWork) : IQueryHandler<ExportarReporteQuery, ArchivoReporteDto>
{
    public async Task<Result<ArchivoReporteDto>> HandleAsync(ExportarReporteQuery query, CancellationToken ct)
    {
        if (!await access.AutorizarAsync(ct)) return Result<ArchivoReporteDto>.Failure(ReporteAccess.Forbidden);
        var reporte = await repository.ConsultarAsync(query.Reporte, ct, query.Formato == "pdf" ? 500 : 10000);
        if (reporte.IsFailure) return Result<ArchivoReporteDto>.Failure(reporte.Errors);
        var archivo = await exporter.ExportarAsync(reporte.Value, query.Formato, ct);
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await audit.WriteAsync(actual.UsuarioId, "EXPORT", "reporte", query.Reporte.Codigo, actual.IpOrigen, token);
                return Result<bool>.Success(true);
            }, ct);
            return Result<ArchivoReporteDto>.Success(archivo);
        }
        catch
        {
            await archivo.Contenido.DisposeAsync();
            throw;
        }
    }
}
