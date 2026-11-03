using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Reporting;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.Administrador)]
[Authorize(Policy = IdentityPolicies.SesionCompleta)]
public sealed class ReportesController(IQueryHandler<ReporteQuery, ReporteDto> consultar) : ApiControllerBase
{
    [HttpGet("{codigo}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Consultar un reporte administrativo con filtros, agrupación y totales")]
    [EndpointDescription("Códigos R-01 a R-15. Solo ADMINISTRADOR habilitado; cliente y dueño: 403. Fechas desde inclusivo/hasta exclusivo (yyyy-MM-dd), días UTC. Filtros no compatibles: 400. Estado corresponde al evento, salvo reportes de compras e IA. Agrupaciones fijas por reporte; totales de todo el conjunto filtrado, separados por moneda. page 1–100000, pageSize 1–100 (20 por defecto). Ejemplo: /api/v1/reportes/R-01?moneda=GTQ&pageSize=20. Ventas brutas y ledger neto se distinguen en nota. Sin caché.")]
    [ProducesResponseType<ReporteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReporteDto>> Obtener(string codigo, [FromQuery] ReporteFiltros filtros, CancellationToken ct) =>
        FromResult(await consultar.HandleAsync(new(codigo, filtros), ct));
}
