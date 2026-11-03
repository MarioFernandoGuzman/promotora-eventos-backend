using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed record CrearLocalidadCommand(int RecintoId, DatosLocalidadDto Datos) : ICommand<LocalidadDto>;
public sealed record ActualizarLocalidadCommand(int RecintoId, int Id, DatosLocalidadDto Datos) : ICommand<LocalidadDto>;
public sealed record BajaLocalidadCommand(int RecintoId, int Id) : ICommand<Unit>;
public sealed record ObtenerLocalidadQuery(int RecintoId, int Id) : IQuery<LocalidadDto>;
public sealed record ListarLocalidadesQuery(int RecintoId, int Page = 1, int PageSize = 20) : IQuery<LocalidadesPaginaDto>;
