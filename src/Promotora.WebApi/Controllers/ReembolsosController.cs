using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Finance;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.Administrador)]
[Authorize(Policy = IdentityPolicies.SesionCompleta)]
public sealed class ReembolsosController(ICommandHandler<ReembolsarCompraCommand, ReembolsoDto> reembolsar) : ApiControllerBase
{
    [HttpPost("/api/v1/compras/{publicId:guid}/reembolsar")]
    [EndpointSummary("Reembolsar completamente una compra; solo administrador habilitado")]
    [EndpointDescription("Sin body. Revierte movimientos originales sin recalcular tasas; pago REEMBOLSADO y compra COMPLETADA. Solo VALIDO pasa a ANULADO y devuelve cupo; USADO conserva historial. 200 también al repetir sin nuevos efectos; 404 inexistente, 409 estado/respaldo/saldo insuficiente/cuenta cerrada. Ejemplo: POST /api/v1/compras/11111111-1111-1111-1111-111111111111/reembolsar. Respuesta: publicId, estadoPago=REEMBOLSADO, montoReembolsado y moneda. No realiza una transferencia externa.")]
    [ProducesResponseType<ReembolsoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReembolsoDto>> Reembolsar(Guid publicId, CancellationToken ct)
        => FromResult(await reembolsar.HandleAsync(new ReembolsarCompraCommand(publicId), ct));
}
