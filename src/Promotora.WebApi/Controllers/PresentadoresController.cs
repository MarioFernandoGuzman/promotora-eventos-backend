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
public sealed class PresentadoresController(ICommandHandler<CrearPresentadorCommand, PresentadorDto> crear,
    ICommandHandler<ActualizarPresentadorCommand, PresentadorDto> actualizar, ICommandHandler<BajaPresentadorCommand, Unit> baja,
    IQueryHandler<ObtenerPresentadorQuery, PresentadorDto> obtener, IQueryHandler<ListarPresentadorQuery, PaginaDto<PresentadorDto>> listar) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [EndpointSummary("Crear presentador; solo administrador")]
    [ProducesResponseType<PresentadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PresentadorDto>> Crear(PresentadorRequest request, CancellationToken ct) => FromResult(await crear.HandleAsync(new CrearPresentadorCommand(request.ToDomain()), ct));

    [HttpGet]
    [EndpointSummary("Listar catálogo paginado, incluidos inactivos; requiere sesión completa")]
    [ProducesResponseType<PaginaDto<PresentadorDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<PresentadorDto>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new ListarPresentadorQuery(page, pageSize), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<PresentadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PresentadorDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerPresentadorQuery(id), ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [ProducesResponseType<PresentadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PresentadorDto>> Actualizar(int id, PresentadorRequest request, CancellationToken ct) => FromResult(await actualizar.HandleAsync(new ActualizarPresentadorCommand(id, request.ToDomain()), ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [EndpointSummary("Baja lógica idempotente; conserva referencias e historial")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Unit>> Baja(int id, CancellationToken ct) => FromResult(await baja.HandleAsync(new BajaPresentadorCommand(id), ct));
}
