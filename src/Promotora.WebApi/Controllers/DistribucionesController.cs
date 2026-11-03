using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Ticketing.Numeracion;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class DistribucionesController(
    ICommandHandler<GuardarDistribucionCommand, DistribucionDto> guardar,
    ICommandHandler<PublicarDistribucionCommand, DistribucionDto> publicar,
    ICommandHandler<BloquearAsientosCommand, Unit> bloquear,
    IQueryHandler<ObtenerDistribucionQuery, DistribucionDto> obtener,
    IQueryHandler<ListarAsientosQuery, DistribucionDto> listar) : ApiControllerBase
{
    [HttpPut("~/api/v1/eventos/{eventoId:int}/localidades/{localidadId:int}/distribucion")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [EndpointSummary("Crear/editar borrador propio; IDs existentes y claves de mesa temporales; máximo 2000 sillas/500 mesas")]
    [ProducesResponseType<DistribucionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DistribucionDto>> Guardar(int eventoId, int localidadId, DatosDistribucion request, CancellationToken ct) =>
        FromResult(await guardar.HandleAsync(new(eventoId, localidadId, request), ct));

    [HttpGet("~/api/v1/eventos/{eventoId:int}/localidades/{localidadId:int}/distribucion")]
    [EndpointSummary("Consultar distribución propia, incluso borrador; mesas y sillas paginadas por ID")]
    [ProducesResponseType<DistribucionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DistribucionDto>> Obtener(int eventoId, int localidadId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await obtener.HandleAsync(new(eventoId, localidadId, page, pageSize), ct));

    [HttpPost("~/api/v1/eventos/{eventoId:int}/localidades/{localidadId:int}/distribucion/publicar")]
    [EndpointSummary("Publicar y congelar distribución; conciliar cupos; no admite historial de compras generales")]
    [ProducesResponseType<DistribucionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DistribucionDto>> Publicar(int eventoId, int localidadId, CancellationToken ct) =>
        FromResult(await publicar.HandleAsync(new(eventoId, localidadId), ct));

    [AllowAnonymous]
    [HttpGet("~/api/v1/eventos/{eventoId:int}/localidades/{localidadId:int}/asientos")]
    [EndpointSummary("Disponibilidad pública paginada de evento visible; no reserva posiciones ni muestra compradores")]
    [ProducesResponseType<DistribucionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DistribucionDto>> Listar(int eventoId, int localidadId, long? mesaId = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new(eventoId, localidadId, mesaId, page, pageSize), ct));

    [HttpPost("~/api/v1/eventos/{eventoId:int}/localidades/{localidadId:int}/asientos/bloquear")]
    [EndpointSummary("Bloquear sillas libres; mesa completa se bloquea íntegra; body lista de IDs")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Unit>> Bloquear(int eventoId, int localidadId, IReadOnlyList<long> asientoIds, CancellationToken ct) =>
        FromResult(await bloquear.HandleAsync(new(eventoId, localidadId, asientoIds, true), ct));

    [HttpPost("~/api/v1/eventos/{eventoId:int}/localidades/{localidadId:int}/asientos/desbloquear")]
    [EndpointSummary("Desbloquear posiciones bloqueadas propias; mesa completa se trata como unidad")]
    [ProducesResponseType<Unit>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Unit>> Desbloquear(int eventoId, int localidadId, IReadOnlyList<long> asientoIds, CancellationToken ct) =>
        FromResult(await bloquear.HandleAsync(new(eventoId, localidadId, asientoIds, false), ct));
}
