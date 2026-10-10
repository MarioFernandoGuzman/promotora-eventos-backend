using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed record CategoriaEventoDto(int Id, string Nombre, string? Descripcion, bool Activo)
{
    public static CategoriaEventoDto From(CategoriaEvento entity) => new(entity.Id, entity.Datos.Nombre, entity.Datos.Descripcion, entity.Activo);
}

public sealed record CrearCategoriaEventoCommand(DatosCategoriaEvento Datos) : ICommand<CategoriaEventoDto>;
public sealed record ActualizarCategoriaEventoCommand(int Id, DatosCategoriaEvento Datos) : ICommand<CategoriaEventoDto>;
public sealed record BajaCategoriaEventoCommand(int Id) : ICommand<Unit>;
public sealed record ObtenerCategoriaEventoQuery(int Id) : IQuery<CategoriaEventoDto>;
public sealed record ListarCategoriaEventoQuery(int Page = 1, int PageSize = 20) : IQuery<PaginaDto<CategoriaEventoDto>>;
