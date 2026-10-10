using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Ticketing;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed record CrearCompraRequest(int EventoId, IReadOnlyList<ItemCompra> Items);

[Authorize(Policy = IdentityPolicies.Cliente)]
[Authorize(Policy = IdentityPolicies.SesionCompleta)]
public sealed class ComprasController(ICommandHandler<CrearCompraCommand, CrearCompraResultado> crear) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(IdentityHttpProtection.Compra)]
    [EndpointSummary("Reservar compra PENDIENTE; precios del catálogo y clave idempotente obligatoria")]
    [EndpointDescription("201 al crear y 200 al reutilizar una clave propia. Esta etapa reserva cupo; pago y emisión se añaden en F5-04/F5-05. En F6-03 el contrato pasará a 202.")]
    [ProducesResponseType<CompraCreadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<CompraCreadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CompraCreadaDto>> Crear(CrearCompraRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey, CancellationToken ct)
    {
        var result = await crear.HandleAsync(new CrearCompraCommand(request.EventoId, request.Items, idempotencyKey), ct);
        if (result.IsFailure) return FromResult(Result<CompraCreadaDto>.Failure(result.Errors));
        return result.Value.Reutilizada ? Ok(result.Value.Compra) : StatusCode(StatusCodes.Status201Created, result.Value.Compra);
    }
}
