using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;
using Promotora.WebApi.Venues;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class RecintosController(ICommandHandler<CrearRecintoCommand, RecintoDto> crear,
    ICommandHandler<ActualizarRecintoCommand, RecintoDto> actualizar, ICommandHandler<BajaRecintoCommand, Unit> baja,
    IQueryHandler<ObtenerRecintoQuery, RecintoDto> obtener, IQueryHandler<ListarRecintosQuery, RecintosPaginaDto> listar,
    IQueryHandler<ObtenerMapaRecintoQuery, MapaRecintoDto> mapa) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("{id:int}/mapa")]
    [EndpointSummary("Mapa público del recinto activo; eventoId opcional añade precios y cupos")]
    [EndpointDescription("Solo localidades y tipos activos. Sin eventoId, cupoDisponible=null; con evento público del recinto, devuelve inventario actual sin reservar. Excluye borradores, cancelados y finalizados.")]
    [ProducesResponseType<MapaRecintoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapaRecintoDto>> Mapa(int id, [FromQuery] int? eventoId = null, CancellationToken ct = default) =>
        FromResult(await mapa.HandleAsync(new ObtenerMapaRecintoQuery(id, eventoId), ct));

    [HttpPost]
    [EndpointSummary("Crear recinto propio o asignarlo a un dueño como administrador")]
    [ProducesResponseType<RecintoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecintoDto>> Crear(CrearRecintoRequest request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new CrearRecintoCommand(request.DuenoId, request.ToDomain()), ct));

    [HttpGet]
    [EndpointSummary("Listar recintos propios; administrador ve todos, incluidos inactivos")]
    [ProducesResponseType<RecintosPaginaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecintosPaginaDto>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new ListarRecintosQuery(page, pageSize), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<RecintoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecintoDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerRecintoQuery(id), ct));

    [HttpPut("{id:int}")]
    [EndpointSummary("Editar recinto activo sin cambiar su dueño; capacidad incluye localidades inactivas")]
    [ProducesResponseType<RecintoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecintoDto>> Actualizar(int id, ActualizarRecintoRequest request, CancellationToken ct) =>
        FromResult(await actualizar.HandleAsync(new ActualizarRecintoCommand(id, request.ToDomain()), ct));

    [HttpDelete("{id:int}")]
    [EndpointSummary("Dar de baja lógicamente; repetir la baja conserva éxito sin duplicar auditoría")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Unit>> Baja(int id, CancellationToken ct) => FromResult(await baja.HandleAsync(new BajaRecintoCommand(id), ct));
}
