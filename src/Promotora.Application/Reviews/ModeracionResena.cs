using System.Globalization;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Reviews;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reviews;

public sealed record ModerarResenaCommand(int Id, string EstadoNuevo) : ICommand<ResenaDto>;
public sealed record ResenasAdministracionQuery(string Estado = "PENDIENTE", int? EventoId = null, int Page = 1, int PageSize = 20) : IQuery<PaginaDto<ResenaDto>>;
public sealed record ResenaParaGestion(int ClienteId, ResenaDto Resena);
public sealed class ModerarResenaValidator : AbstractValidator<ModerarResenaCommand>
{
    public ModerarResenaValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EstadoNuevo).Must(e => e is "APROBADA" or "RECHAZADA");
    }
}
public sealed class ResenasAdministracionValidator : AbstractValidator<ResenasAdministracionQuery>
{
    public ResenasAdministracionValidator()
    {
        RuleFor(x => x.Estado).Must(EstadoResena.Existe);
        RuleFor(x => x.EventoId).GreaterThan(0).When(x => x.EventoId.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class ModerarResenaHandler(IResenaRepository resenas, ResenaAccess acceso, IUnitOfWork trabajo, IAuditLogger audit, IUsuarioActual actual)
    : ICommandHandler<ModerarResenaCommand, ResenaDto>
{
    public Task<Result<ResenaDto>> HandleAsync(ModerarResenaCommand request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (!await acceso.RolAsync(RolUsuario.ADMINISTRADOR, token)) return Result<ResenaDto>.Failure(ResenaAccess.Forbidden);
        var bloqueada = await resenas.LockAsync(request.Id, token);
        if (bloqueada is null) return Result<ResenaDto>.Failure(ResenaAccess.NotFound);
        var cambio = EstadoResena.Moderar(bloqueada.Resena.Estado, request.EstadoNuevo);
        if (cambio.IsFailure) return Result<ResenaDto>.Failure(cambio.Errors);
        if (bloqueada.Resena.Estado == request.EstadoNuevo) return Result<ResenaDto>.Success(bloqueada.Resena);
        if (!await resenas.CambiarEstadoAsync(request.Id, "PENDIENTE", request.EstadoNuevo, token))
            return Result<ResenaDto>.Failure(new Error("resena.conflicto", "La reseña cambió de estado.", ErrorType.Conflict));
        await audit.WriteAsync(actual.UsuarioId, "MODERATE", "resenas", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<ResenaDto>.Success(bloqueada.Resena with { Estado = request.EstadoNuevo });
    }, ct);
}
public sealed class ResenasAdministracionHandler(IResenaRepository resenas, ResenaAccess acceso, IUnitOfWork trabajo)
    : IQueryHandler<ResenasAdministracionQuery, PaginaDto<ResenaDto>>
{
    public Task<Result<PaginaDto<ResenaDto>>> HandleAsync(ResenasAdministracionQuery request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (!await acceso.RolAsync(RolUsuario.ADMINISTRADOR, token)) return Result<PaginaDto<ResenaDto>>.Failure(ResenaAccess.Forbidden);
        return Result<PaginaDto<ResenaDto>>.Success(await resenas.AdministracionAsync(request, token));
    }, ct);
}
