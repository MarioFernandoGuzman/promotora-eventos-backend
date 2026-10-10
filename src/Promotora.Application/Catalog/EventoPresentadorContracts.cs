using FluentValidation;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed record EventoPresentadorDto(int EventoId, int PresentadorId, string Nombre, string Tipo, string? Rol, bool Activo);
public sealed record AsociarPresentadorCommand(int EventoId, int PresentadorId, string? Rol) : ICommand<EventoPresentadorDto>;
public sealed record EditarRolPresentadorCommand(int EventoId, int PresentadorId, string? Rol) : ICommand<EventoPresentadorDto>;
public sealed record RetirarPresentadorCommand(int EventoId, int PresentadorId) : ICommand<Unit>;
public sealed record ListarEventoPresentadoresQuery(int EventoId, int Page = 1, int PageSize = 20) : IQuery<PaginaDto<EventoPresentadorDto>>;

public sealed class AsociarPresentadorValidator : AbstractValidator<AsociarPresentadorCommand>
{
    public AsociarPresentadorValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.PresentadorId).GreaterThan(0); RuleFor(x => x.Rol).MaximumLength(30); }
}
public sealed class EditarRolPresentadorValidator : AbstractValidator<EditarRolPresentadorCommand>
{
    public EditarRolPresentadorValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.PresentadorId).GreaterThan(0); RuleFor(x => x.Rol).MaximumLength(30); }
}
public sealed class ListarEventoPresentadoresValidator : AbstractValidator<ListarEventoPresentadoresQuery>
{
    public ListarEventoPresentadoresValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100); }
}
