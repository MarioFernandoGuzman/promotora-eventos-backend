using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;
using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed record CrearRecintoCommand(int? DuenoId, DatosRecinto Datos) : ICommand<RecintoDto>;
public sealed record ActualizarRecintoCommand(int Id, DatosRecinto Datos) : ICommand<RecintoDto>;
public sealed record BajaRecintoCommand(int Id) : ICommand<Unit>;
public sealed record ObtenerRecintoQuery(int Id) : IQuery<RecintoDto>;
public sealed record ListarRecintosQuery(int Page = 1, int PageSize = 20) : IQuery<RecintosPaginaDto>;
