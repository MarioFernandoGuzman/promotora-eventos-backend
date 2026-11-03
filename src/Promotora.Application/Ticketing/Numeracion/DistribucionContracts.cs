using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing.Numeracion;

public sealed record MesaConfiguracion(long? Id, string Clave, string Etiqueta, string Modalidad, int TipoEntradaId, decimal X, decimal Y);
public sealed record AsientoConfiguracion(long? Id, string? MesaClave, string Etiqueta, string? Fila, int TipoEntradaId, decimal X, decimal Y, bool Bloqueado = false);
public sealed record DatosDistribucion(string? ImagenPlanoUrl, IReadOnlyList<MesaConfiguracion> Mesas, IReadOnlyList<AsientoConfiguracion> Asientos);
public sealed record MesaPlanoDto(long Id, string Etiqueta, string Modalidad, int TipoEntradaId, decimal X, decimal Y, int Capacidad);
public sealed record AsientoPlanoDto(long Id, long? MesaId, string Etiqueta, string? Fila, int TipoEntradaId, decimal X, decimal Y, string Estado);
public sealed record DistribucionDto(long Id, int EventoId, int LocalidadId, string Estado, string? ImagenPlanoUrl,
    int Page, int PageSize, int TotalMesas, int TotalAsientos, IReadOnlyList<MesaPlanoDto> Mesas, IReadOnlyList<AsientoPlanoDto> Asientos);
public sealed record ContextoDistribucion(int DuenoId, bool Activo, bool Publico);
public sealed record GuardarDistribucionCommand(int EventoId, int LocalidadId, DatosDistribucion Datos) : ICommand<DistribucionDto>;
public sealed record PublicarDistribucionCommand(int EventoId, int LocalidadId) : ICommand<DistribucionDto>;
public sealed record BloquearAsientosCommand(int EventoId, int LocalidadId, IReadOnlyList<long> AsientoIds, bool Bloquear) : ICommand<Unit>;
public sealed record ObtenerDistribucionQuery(int EventoId, int LocalidadId, int Page = 1, int PageSize = 20) : IQuery<DistribucionDto>;
public sealed record ListarAsientosQuery(int EventoId, int LocalidadId, long? MesaId = null, int Page = 1, int PageSize = 20) : IQuery<DistribucionDto>;

public interface IDistribucionRepository
{
    Task<ContextoDistribucion?> ContextoAsync(int eventoId, int localidadId, bool bloquear, CancellationToken ct);
    Task<DistribucionDto?> ObtenerAsync(int eventoId, int localidadId, bool publico, int page, int pageSize, long? mesaId, CancellationToken ct);
    Task<Result<DistribucionDto>> GuardarAsync(GuardarDistribucionCommand command, CancellationToken ct);
    Task<Result<DistribucionDto>> PublicarAsync(PublicarDistribucionCommand command, CancellationToken ct);
    Task<Result<Unit>> BloquearAsync(BloquearAsientosCommand command, CancellationToken ct);
}
