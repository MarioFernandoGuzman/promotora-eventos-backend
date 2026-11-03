using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;
using Promotora.WebApi.Catalog;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class TiposEntradaController(ICommandHandler<CrearTipoEntradaCommand, TipoEntradaDto> crear,
    ICommandHandler<EditarTipoEntradaCommand, TipoEntradaDto> editar, ICommandHandler<BajaTipoEntradaCommand, Unit> baja,
    IQueryHandler<ObtenerTipoEntradaQuery, TipoEntradaDto> obtener, IQueryHandler<ListarTiposEntradaQuery, PaginaDto<TipoEntradaDto>> listar) : ApiControllerBase
{
    [HttpPost("~/api/v1/eventos/{eventoId:int}/tipos-entrada")]
    [EndpointSummary("Configurar entrada general, por localidad o distancia; disponibilidad inicial = cupo total")]
    [ProducesResponseType<TipoEntradaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TipoEntradaDto>> Crear(int eventoId, CrearTipoEntradaRequest request, CancellationToken ct) => FromResult(await crear.HandleAsync(new CrearTipoEntradaCommand(eventoId, request.LocalidadId, request.ToDomain()), ct));
    [HttpPut("~/api/v1/eventos/{eventoId:int}/tipos-entrada/{id:int}")]
    [EndpointSummary("Editar antes de ventas; ajuste atómico de cupo, localidad inmutable")]
    [ProducesResponseType<TipoEntradaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TipoEntradaDto>> Editar(int eventoId, int id, EditarTipoEntradaRequest request, CancellationToken ct) => FromResult(await editar.HandleAsync(new EditarTipoEntradaCommand(eventoId, id, request.ToDomain()), ct));
    [HttpDelete("~/api/v1/eventos/{eventoId:int}/tipos-entrada/{id:int}")]
    [EndpointSummary("Baja lógica antes de ventas; sigue contando para la capacidad de su localidad")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Unit>> Baja(int eventoId, int id, CancellationToken ct) => FromResult(await baja.HandleAsync(new BajaTipoEntradaCommand(eventoId, id), ct));
    [HttpGet("~/api/v1/eventos/{eventoId:int}/tipos-entrada/{id:int}")]
    [ProducesResponseType<TipoEntradaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TipoEntradaDto>> Obtener(int eventoId, int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerTipoEntradaQuery(eventoId, id), ct));
    [HttpGet("~/api/v1/eventos/{eventoId:int}/tipos-entrada")]
    [ProducesResponseType<PaginaDto<TipoEntradaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<TipoEntradaDto>>> Listar(int eventoId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => FromResult(await listar.HandleAsync(new ListarTiposEntradaQuery(eventoId, page, pageSize), ct));
}
