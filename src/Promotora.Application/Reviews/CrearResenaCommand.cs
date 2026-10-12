using System.Globalization;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Reviews;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reviews;

public sealed record ResenaDto(int Id, int EventoId, short Calificacion, string? Comentario, string Estado, DateTime FechaCreacion);
public sealed record CrearResenaCommand(int EventoId, short Calificacion, string? Comentario) : ICommand<ResenaDto>;
public interface IResenaRepository
{
    Task<CalificacionPresentadorDto?> CalificacionAsync(int presentadorId, CancellationToken ct);
    Task<ResenaParaGestion?> LockAsync(int id, CancellationToken ct);
    Task<bool> CambiarEstadoAsync(int id, string esperado, string nuevo, CancellationToken ct);
    Task<Promotora.Application.Shared.PaginaDto<ResenaDto>> AdministracionAsync(ResenasAdministracionQuery query, CancellationToken ct);
    Task<int?> ClienteIdAsync(int usuarioId, CancellationToken ct);
    Task<bool> EventoExisteAsync(int eventoId, CancellationToken ct);
    Task<bool> AsistioAsync(int clienteId, int eventoId, CancellationToken ct);
    Task<ResenaDto?> CrearAsync(int clienteId, int eventoId, ContenidoResena contenido, DateTime fecha, CancellationToken ct);
}
public sealed class CrearResenaValidator : AbstractValidator<CrearResenaCommand>
{
    public CrearResenaValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0);
        RuleFor(x => x.Calificacion).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.Comentario).MaximumLength(510);
    }
}
public sealed class ResenaAccess(IUsuarioActual actual, ISesionRepository sesiones, IUsuarioRepository usuarios, IResenaRepository resenas)
{
    public async Task<bool> RolAsync(RolUsuario rol, CancellationToken ct)
    {
        if (actual.UsuarioId is not int id) return false;
        await sesiones.LockUsuarioAsync(id, ct);
        var usuario = await usuarios.FindByIdAsync(id, ct);
        return usuario is not null && usuario.PuedeAutenticarse && !usuario.DebeCambiarPassword && usuario.Rol == rol;
    }
    public async Task<int?> ClienteAsync(CancellationToken ct) => await RolAsync(RolUsuario.CLIENTE, ct)
        ? await resenas.ClienteIdAsync(actual.UsuarioId!.Value, ct) : null;
    public static Error Forbidden { get; } = new("resena.prohibida", "Usuario no habilitado para esta operación.", ErrorType.Forbidden);
    public static Error NotFound { get; } = new("resena.no_encontrada", "Reseña no encontrada.", ErrorType.NotFound);
}
public sealed class CrearResenaHandler(IResenaRepository resenas, ResenaAccess acceso, IUnitOfWork trabajo, IUsuarioActual actual,
    IClock clock, IAuditLogger audit) : ICommandHandler<CrearResenaCommand, ResenaDto>
{
    public Task<Result<ResenaDto>> HandleAsync(CrearResenaCommand request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (await acceso.ClienteAsync(token) is not int cliente) return Result<ResenaDto>.Failure(ResenaAccess.Forbidden);
        var contenido = ContenidoResena.Crear(request.Calificacion, request.Comentario);
        if (contenido.IsFailure) return Result<ResenaDto>.Failure(contenido.Errors);
        if (!await resenas.EventoExisteAsync(request.EventoId, token))
            return Result<ResenaDto>.Failure(new Error("evento.no_encontrado", "Evento no encontrado.", ErrorType.NotFound));
        if (!await resenas.AsistioAsync(cliente, request.EventoId, token))
            return Result<ResenaDto>.Failure(new Error("resena.asistencia", "Se requiere un boleto USADO de este evento.", ErrorType.BusinessRule));
        var creada = await resenas.CrearAsync(cliente, request.EventoId, contenido.Value, clock.UtcNow, token);
        if (creada is null) return Result<ResenaDto>.Failure(new Error("resena.duplicada", "Ya existe una reseña de este cliente para el evento.", ErrorType.Conflict));
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "resenas", creada.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<ResenaDto>.Success(creada);
    }, ct);
}
