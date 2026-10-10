using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class LocalidadesController(ICommandHandler<CrearLocalidadCommand, LocalidadDto> crear,
    ICommandHandler<ActualizarLocalidadCommand, LocalidadDto> actualizar, ICommandHandler<BajaLocalidadCommand, Unit> baja,
    IQueryHandler<ObtenerLocalidadQuery, LocalidadDto> obtener, IQueryHandler<ListarLocalidadesQuery, LocalidadesPaginaDto> listar) : ApiControllerBase
{
    [HttpPost("~/api/v1/recintos/{recintoId:int}/localidades")]
    [EndpointSummary("Crear zona del recinto propio o como administrador; suma de capacidades protegida")]
    [ProducesResponseType<LocalidadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LocalidadDto>> Crear(int recintoId, DatosLocalidadDto request, CancellationToken ct) =>
        FromResult(await crear.HandleAsync(new CrearLocalidadCommand(recintoId, request), ct));

    [HttpGet("~/api/v1/recintos/{recintoId:int}/localidades")]
    [EndpointSummary("Listar zonas del recinto, incluidas inactivas, con paginación")]
    [ProducesResponseType<LocalidadesPaginaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocalidadesPaginaDto>> Listar(int recintoId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new ListarLocalidadesQuery(recintoId, page, pageSize), ct));

    [HttpGet("~/api/v1/recintos/{recintoId:int}/localidades/{id:int}")]
    [ProducesResponseType<LocalidadDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LocalidadDto>> Obtener(int recintoId, int id, CancellationToken ct) =>
        FromResult(await obtener.HandleAsync(new ObtenerLocalidadQuery(recintoId, id), ct));

    [HttpPut("~/api/v1/recintos/{recintoId:int}/localidades/{id:int}")]
    [EndpointSummary("Editar zona activa, polígono y color sin cambiar recinto; protege cupos por evento")]
    [ProducesResponseType<LocalidadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LocalidadDto>> Actualizar(int recintoId, int id, DatosLocalidadDto request, CancellationToken ct) =>
        FromResult(await actualizar.HandleAsync(new ActualizarLocalidadCommand(recintoId, id, request), ct));

    [HttpDelete("~/api/v1/recintos/{recintoId:int}/localidades/{id:int}")]
    [EndpointSummary("Baja lógica idempotente; la capacidad de esta zona sigue contando")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Unit>> Baja(int recintoId, int id, CancellationToken ct) =>
        FromResult(await baja.HandleAsync(new BajaLocalidadCommand(recintoId, id), ct));
}
