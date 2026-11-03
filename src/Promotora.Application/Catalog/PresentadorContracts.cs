using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed record PresentadorDto(int Id, string Nombre, string Tipo, string? Biografia, string? ImagenUrl, bool Activo)
{
    public static PresentadorDto From(Presentador entity) => new(entity.Id, entity.Datos.Nombre, entity.Datos.Tipo, entity.Datos.Biografia, entity.Datos.ImagenUrl, entity.Activo);
}

public sealed record CrearPresentadorCommand(DatosPresentador Datos) : ICommand<PresentadorDto>;
public sealed record ActualizarPresentadorCommand(int Id, DatosPresentador Datos) : ICommand<PresentadorDto>;
public sealed record BajaPresentadorCommand(int Id) : ICommand<Unit>;
public sealed record ObtenerPresentadorQuery(int Id) : IQuery<PresentadorDto>;
public sealed record ListarPresentadorQuery(int Page = 1, int PageSize = 20) : IQuery<PaginaDto<PresentadorDto>>;
