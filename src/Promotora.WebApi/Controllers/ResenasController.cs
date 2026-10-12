using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Reviews;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed record CrearResenaRequest(int EventoId, short Calificacion, string? Comentario);
public sealed class ResenasController(ICommandHandler<CrearResenaCommand, ResenaDto> crear) : ApiControllerBase
{
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
