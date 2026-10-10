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
public sealed class EventosController(ICommandHandler<CrearEventoCommand, EventoDto> crear,
    ICommandHandler<ActualizarEventoCommand, EventoDto> editar, ICommandHandler<CambiarEstadoEventoCommand, EventoDto> estado,
    ICommandHandler<CancelarEventoCommand, Unit> cancelar, IQueryHandler<ObtenerEventoQuery, EventoDto> obtener,
    IQueryHandler<ListarEventosQuery, PaginaDto<EventoDto>> listar) : ApiControllerBase
{
    [HttpPost]
    [EndpointSummary("Crear BORRADOR; creador de la sesión, dueño del recinto o administrador")]
    [ProducesResponseType<EventoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventoDto>> Crear(EventoRequest request, CancellationToken ct) => FromResult(await crear.HandleAsync(new CrearEventoCommand(request.ToDomain()), ct));

    [HttpGet]
    [EndpointSummary("Gestión privada paginada; dueño solo sus eventos, administrador todos")]
    [ProducesResponseType<PaginaDto<EventoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<EventoDto>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => FromResult(await listar.HandleAsync(new ListarEventosQuery(page, pageSize), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<EventoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventoDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerEventoQuery(id), ct));

    [HttpPut("{id:int}")]
    [EndpointSummary("Configurar BORRADOR/PROGRAMADO sin compras; recinto y creador inmutables")]
    [ProducesResponseType<EventoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventoDto>> Editar(int id, EventoRequest request, CancellationToken ct) => FromResult(await editar.HandleAsync(new ActualizarEventoCommand(id, request.ToDomain()), ct));

    [HttpPut("{id:int}/estado")]
    [EndpointSummary("Transición condicional; venta con ventana vigente y cupo")]
    [ProducesResponseType<EventoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventoDto>> Estado(int id, EstadoEventoRequest request, CancellationToken ct) => FromResult(await estado.HandleAsync(new CambiarEstadoEventoCommand(id, request.EstadoEsperado, request.EstadoNuevo), ct));

    [HttpDelete("{id:int}")]
    [EndpointSummary("Cancelar sin compras; baja lógica idempotente, no reembolsa")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Unit>> Cancelar(int id, CancellationToken ct) => FromResult(await cancelar.HandleAsync(new CancelarEventoCommand(id), ct));
}
