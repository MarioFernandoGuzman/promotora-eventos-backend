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
public sealed class CategoriasController(ICommandHandler<CrearCategoriaEventoCommand, CategoriaEventoDto> crear,
    ICommandHandler<ActualizarCategoriaEventoCommand, CategoriaEventoDto> actualizar, ICommandHandler<BajaCategoriaEventoCommand, Unit> baja,
    IQueryHandler<ObtenerCategoriaEventoQuery, CategoriaEventoDto> obtener, IQueryHandler<ListarCategoriaEventoQuery, PaginaDto<CategoriaEventoDto>> listar) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [EndpointSummary("Crear categoría; solo administrador")]
    [ProducesResponseType<CategoriaEventoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaEventoDto>> Crear(CategoriaEventoRequest request, CancellationToken ct) => FromResult(await crear.HandleAsync(new CrearCategoriaEventoCommand(request.ToDomain()), ct));

    [HttpGet]
    [EndpointSummary("Listar catálogo paginado, incluidos inactivos; requiere sesión completa")]
    [ProducesResponseType<PaginaDto<CategoriaEventoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<CategoriaEventoDto>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new ListarCategoriaEventoQuery(page, pageSize), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<CategoriaEventoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaEventoDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerCategoriaEventoQuery(id), ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [ProducesResponseType<CategoriaEventoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaEventoDto>> Actualizar(int id, CategoriaEventoRequest request, CancellationToken ct) => FromResult(await actualizar.HandleAsync(new ActualizarCategoriaEventoCommand(id, request.ToDomain()), ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [EndpointSummary("Baja lógica idempotente; conserva referencias e historial")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Unit>> Baja(int id, CancellationToken ct) => FromResult(await baja.HandleAsync(new BajaCategoriaEventoCommand(id), ct));
}
