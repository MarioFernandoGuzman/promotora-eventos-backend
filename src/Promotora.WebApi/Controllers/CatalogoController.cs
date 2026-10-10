using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;

namespace Promotora.WebApi.Controllers;

public sealed class CatalogoController(IQueryHandler<ListarCatalogoPublicoQuery, PaginaDto<EventoResumenPublicoDto>> listar,
    IQueryHandler<ObtenerEventoPublicoQuery, EventoPublicoDto> obtener) : ApiControllerBase
{
    [HttpGet("eventos")]
    [AllowAnonymous]
    [EndpointSummary("Catálogo público: PROGRAMADO/EN_VENTA/AGOTADO, filtros combinables y paginación")]
    [ProducesResponseType<PaginaDto<EventoResumenPublicoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<EventoResumenPublicoDto>>> Listar([FromQuery] int? categoriaId = null, [FromQuery] string? ciudad = null,
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, [FromQuery] string? texto = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new ListarCatalogoPublicoQuery(categoriaId, ciudad, desde, hasta, texto, page, pageSize), ct));

    [HttpGet("eventos/{id:int}")]
    [AllowAnonymous]
    [EndpointSummary("Detalle público con recinto, categoría, presentadores y tipos activos; no contiene usuarios ni finanzas")]
    [ProducesResponseType<EventoPublicoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventoPublicoDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new ObtenerEventoPublicoQuery(id), ct));
}
