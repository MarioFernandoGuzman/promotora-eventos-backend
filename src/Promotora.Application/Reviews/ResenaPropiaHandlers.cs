using System.Globalization;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Reviews;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reviews;

public sealed record MisResenasQuery(int Page = 1, int PageSize = 20) : IQuery<PaginaDto<ResenaDto>>;
public sealed record EditarResenaCommand(int Id, short Calificacion, string? Comentario, string EstadoEsperado) : ICommand<ResenaDto>;
public sealed record RetirarResenaCommand(int Id, string EstadoEsperado) : ICommand<ResenaDto>;
public sealed class MisResenasValidator : AbstractValidator<MisResenasQuery>
{
    public MisResenasValidator() { RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100); }
}
public sealed class EditarResenaValidator : AbstractValidator<EditarResenaCommand>
{
    public EditarResenaValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0); RuleFor(x => x.Calificacion).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.Comentario).MaximumLength(510); RuleFor(x => x.EstadoEsperado).Must(EstadoResena.Existe);
    }
}
public sealed class RetirarResenaValidator : AbstractValidator<RetirarResenaCommand>
{
    public RetirarResenaValidator() { RuleFor(x => x.Id).GreaterThan(0); RuleFor(x => x.EstadoEsperado).Must(EstadoResena.Existe); }
}
public sealed class MisResenasHandler(IResenaRepository resenas, ResenaAccess acceso, IUnitOfWork trabajo) : IQueryHandler<MisResenasQuery, PaginaDto<ResenaDto>>
{
    public Task<Result<PaginaDto<ResenaDto>>> HandleAsync(MisResenasQuery request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (await acceso.ClienteAsync(token) is not int cliente) return Result<PaginaDto<ResenaDto>>.Failure(ResenaAccess.Forbidden);
        return Result<PaginaDto<ResenaDto>>.Success(await resenas.MisAsync(cliente, request.Page, request.PageSize, token));
    }, ct);
}
public sealed class EditarResenaHandler(IResenaRepository resenas, ResenaAccess acceso, IUnitOfWork trabajo, IAuditLogger audit, IUsuarioActual actual)
    : ICommandHandler<EditarResenaCommand, ResenaDto>
{
    public Task<Result<ResenaDto>> HandleAsync(EditarResenaCommand request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (await acceso.ClienteAsync(token) is not int cliente) return Result<ResenaDto>.Failure(ResenaAccess.Forbidden);
        var fila = await resenas.LockAsync(request.Id, token);
        if (fila is null || fila.ClienteId != cliente) return Result<ResenaDto>.Failure(ResenaAccess.NotFound);
        var contenido = ContenidoResena.Crear(request.Calificacion, request.Comentario);
        if (contenido.IsFailure) return Result<ResenaDto>.Failure(contenido.Errors);
        bool igual = fila.Resena.Calificacion == contenido.Value.Calificacion && fila.Resena.Comentario == contenido.Value.Comentario;
        var cambio = EstadoResena.Editar(fila.Resena.Estado, request.EstadoEsperado, igual);
        if (cambio.IsFailure) return Result<ResenaDto>.Failure(cambio.Errors);
        if (fila.Resena.Estado == "PENDIENTE" && igual) return Result<ResenaDto>.Success(fila.Resena);
        if (!await resenas.EditarAsync(request.Id, cliente, fila.Resena.Estado, contenido.Value, token))
            return Result<ResenaDto>.Failure(new Error("resena.conflicto", "La reseña cambió de estado.", ErrorType.Conflict));
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "resenas", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, new DetalleAuditoria(MotivoAuditoria.ResenaEditada), token);
        return Result<ResenaDto>.Success(fila.Resena with { Calificacion = contenido.Value.Calificacion, Comentario = contenido.Value.Comentario, Estado = "PENDIENTE" });
    }, ct);
}
public sealed class RetirarResenaHandler(IResenaRepository resenas, ResenaAccess acceso, IUnitOfWork trabajo, IAuditLogger audit, IUsuarioActual actual)
    : ICommandHandler<RetirarResenaCommand, ResenaDto>
{
    public Task<Result<ResenaDto>> HandleAsync(RetirarResenaCommand request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (await acceso.ClienteAsync(token) is not int cliente) return Result<ResenaDto>.Failure(ResenaAccess.Forbidden);
        var fila = await resenas.LockAsync(request.Id, token);
        if (fila is null || fila.ClienteId != cliente) return Result<ResenaDto>.Failure(ResenaAccess.NotFound);
        var cambio = EstadoResena.Retirar(fila.Resena.Estado, request.EstadoEsperado);
        if (cambio.IsFailure) return Result<ResenaDto>.Failure(cambio.Errors);
        if (fila.Resena.Estado == "RECHAZADA") return Result<ResenaDto>.Success(fila.Resena);
        if (!await resenas.CambiarEstadoAsync(request.Id, fila.Resena.Estado, "RECHAZADA", token))
            return Result<ResenaDto>.Failure(new Error("resena.conflicto", "La reseña cambió de estado.", ErrorType.Conflict));
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "resenas", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, new DetalleAuditoria(MotivoAuditoria.ResenaRetirada), token);
        return Result<ResenaDto>.Success(fila.Resena with { Estado = "RECHAZADA" });
    }, ct);
}
