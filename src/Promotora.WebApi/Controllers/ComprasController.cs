using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Ticketing;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed record CrearCompraRequest(int EventoId, IReadOnlyList<ItemCompra> Items);

[Authorize(Policy = IdentityPolicies.Cliente)]
[Authorize(Policy = IdentityPolicies.SesionCompleta)]
public sealed class ComprasController(ICommandHandler<CrearCompraCommand, CrearCompraResultado> crear,
    IQueryHandler<ObtenerCompraQuery, CompraCreadaDto> obtener, IQueryHandler<MisComprasQuery, PaginaDto<CompraCreadaDto>> listar,
    ICommandHandler<CancelarCompraCommand, CompraCreadaDto> cancelar) : ApiControllerBase
{
    [HttpGet("{publicId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Consultar una compra propia por publicId; polling de su estado")]
    [ProducesResponseType<CompraCreadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompraCreadaDto>> Obtener(Guid publicId, CancellationToken ct) =>
        FromResult(await obtener.HandleAsync(new ObtenerCompraQuery(publicId), ct));

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Mis compras, incluidas las canceladas; paginación de 1 a 100 filas")]
    [ProducesResponseType<PaginaDto<CompraCreadaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaDto<CompraCreadaDto>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new MisComprasQuery(page, pageSize), ct));

    [HttpPost("{publicId:guid}/cancelar")]
    [EndpointSummary("Cancelar compra propia PENDIENTE y devolver cupo una sola vez")]
    [EndpointDescription("200 también si ya está CANCELADA. PROCESANDO u otro estado devuelve 409; compra ajena/inexistente devuelve 404. Auditoría y devolución comparten transacción.")]
    [ProducesResponseType<CompraCreadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CompraCreadaDto>> Cancelar(Guid publicId, CancellationToken ct) =>
        FromResult(await cancelar.HandleAsync(new CancelarCompraCommand(publicId), ct));

    [HttpPost]
    [EnableRateLimiting(IdentityHttpProtection.Compra)]
    [EndpointSummary("Reservar compra PENDIENTE; precios del catálogo y clave idempotente obligatoria")]
    [EndpointDescription("Reserva + detalles + auditoría + outbox en una transacción. 202 con Location para PENDIENTE/PROCESANDO, también al repetir la clave; 200 al reutilizar un estado final. Worker cobra y emite de forma asíncrona. Asistentes opcionales por ítem, uno por unidad; nombre y documento obligatorios en carreras. No enviar datos de tarjeta.")]
    [ProducesResponseType<CompraCreadaDto>(StatusCodes.Status202Accepted)]
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
        if (result.Value.ProcesamientoDisponible && result.Value.Compra.Estado is "PENDIENTE" or "PROCESANDO")
            return AcceptedAtAction(nameof(Obtener), new { publicId = result.Value.Compra.PublicId }, result.Value.Compra);
        return Ok(result.Value.Compra);
    }
}
