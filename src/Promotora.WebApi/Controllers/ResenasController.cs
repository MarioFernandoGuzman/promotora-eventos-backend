using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Reviews;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed record CrearResenaRequest(int EventoId, short Calificacion, string? Comentario);
public sealed class ResenasController(ICommandHandler<CrearResenaCommand, ResenaDto> crear,
    ICommandHandler<ModerarResenaCommand, ResenaDto> moderar,
    IQueryHandler<ResenasAdministracionQuery, Promotora.Application.Shared.PaginaDto<ResenaDto>> administracion,
    IQueryHandler<ResenasEventoQuery, Promotora.Application.Shared.PaginaDto<ResenaDto>> publicas) : ApiControllerBase
{
    [HttpGet("/api/v1/eventos/{id:int}/resenas")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Reseñas APROBADA de un evento; acceso público")]
    [EndpointDescription("Solo aprobadas, sin datos de identidad del cliente. page 1–100000, pageSize 1–100 (default 20), orden fechaCreacion/ID descendentes. Evento existente sin aprobadas: 200 lista vacía; inexistente: 404. Ejemplo: GET /api/v1/eventos/1/resenas?page=1&pageSize=20.")]
    [ProducesResponseType<Promotora.Application.Shared.PaginaDto<ResenaDto>>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<Promotora.Application.Shared.PaginaDto<ResenaDto>>> Publicas(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => FromResult(await publicas.HandleAsync(new(id, page, pageSize), ct));

    public sealed record ModerarResenaRequest(string EstadoNuevo);
    [HttpGet]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [EndpointSummary("Cola administrativa de reseñas; PENDIENTE por defecto")]
    [EndpointDescription("Filtros estado PENDIENTE/APROBADA/RECHAZADA y eventoId opcional. page 1–100000, pageSize 1–100 (default 20). Orden fechaCreacion/ID descendentes. No publica datos de cliente.")]
    [ProducesResponseType<Promotora.Application.Shared.PaginaDto<ResenaDto>>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    public async Task<ActionResult<Promotora.Application.Shared.PaginaDto<ResenaDto>>> Administracion([FromQuery] ResenasAdministracionQuery query, CancellationToken ct) => FromResult(await administracion.HandleAsync(query, ct));

    [HttpPatch("{id:int}/moderar")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [EndpointSummary("Aprobar o rechazar reseña PENDIENTE")]
    [EndpointDescription("Body: {\"estadoNuevo\":\"APROBADA\"} o RECHAZADA. Repetir misma decisión devuelve 200 sin otra auditoría. Decisión incompatible con estado final: 409. Inexistente: 404. Audita MODERATE en la misma transacción.")]
    [ProducesResponseType<ResenaDto>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<ResenaDto>> Moderar(int id, ModerarResenaRequest request, CancellationToken ct) => FromResult(await moderar.HandleAsync(new(id, request.EstadoNuevo), ct));

    [HttpPost]
    [Authorize(Policy = IdentityPolicies.Cliente)]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [EndpointSummary("Crear reseña PENDIENTE tras asistir a un evento")]
    [EndpointDescription("Body: {\"eventoId\":1,\"calificacion\":5,\"comentario\":\"Excelente evento\"}. CLIENTE habilitado con boleto USADO del evento. Calificación 1–5, comentario opcional de máximo 510 caracteres. Una por cliente/evento: duplicado 409; sin asistencia 422; evento inexistente 404. No publica hasta moderación. No acepta clienteId del usuario.")]
    [ProducesResponseType<ResenaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ResenaDto>> Crear(CrearResenaRequest request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new(request.EventoId, request.Calificacion, request.Comentario), ct));
}
