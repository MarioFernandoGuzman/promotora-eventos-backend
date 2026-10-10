using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class EventoPresentadoresController(ICommandHandler<AsociarPresentadorCommand, EventoPresentadorDto> asociar,
    ICommandHandler<EditarRolPresentadorCommand, EventoPresentadorDto> editar, ICommandHandler<RetirarPresentadorCommand, Unit> retirar,
    IQueryHandler<ListarEventoPresentadoresQuery, PaginaDto<EventoPresentadorDto>> listar) : ApiControllerBase
{
    [HttpPost("~/api/v1/eventos/{eventoId:int}/presentadores")]
    [EndpointSummary("Asociar presentador activo a evento propio BORRADOR/PROGRAMADO sin compras")]
    [ProducesResponseType<EventoPresentadorDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventoPresentadorDto>> Asociar(int eventoId, AsociarPresentadorRequest request, CancellationToken ct) => FromResult(await asociar.HandleAsync(new AsociarPresentadorCommand(eventoId, request.PresentadorId, request.Rol), ct));
    [HttpPut("~/api/v1/eventos/{eventoId:int}/presentadores/{presentadorId:int}")]
    [ProducesResponseType<EventoPresentadorDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventoPresentadorDto>> Editar(int eventoId, int presentadorId, RolPresentadorRequest request, CancellationToken ct) => FromResult(await editar.HandleAsync(new EditarRolPresentadorCommand(eventoId, presentadorId, request.Rol), ct));
    [HttpDelete("~/api/v1/eventos/{eventoId:int}/presentadores/{presentadorId:int}")]
    [EndpointSummary("Retirar asociación idempotentemente; conserva evento y presentador")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Unit>> Retirar(int eventoId, int presentadorId, CancellationToken ct) => FromResult(await retirar.HandleAsync(new RetirarPresentadorCommand(eventoId, presentadorId), ct));
    [HttpGet("~/api/v1/eventos/{eventoId:int}/presentadores")]
    [ProducesResponseType<PaginaDto<EventoPresentadorDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<EventoPresentadorDto>>> Listar(int eventoId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => FromResult(await listar.HandleAsync(new ListarEventoPresentadoresQuery(eventoId, page, pageSize), ct));
}

public sealed record AsociarPresentadorRequest(int PresentadorId, string? Rol = null);
public sealed record RolPresentadorRequest(string? Rol = null);
