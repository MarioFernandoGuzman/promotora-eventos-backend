using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Finance;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[Authorize(Roles = "ADMINISTRADOR,DUENO_RECINTO")]
public sealed class CuentasController(ICommandHandler<CerrarCuentaCommand, CuentaDto> cerrar) : ApiControllerBase
{
    [HttpPost("{id:int}/cerrar")]
    [EndpointSummary("Cerrar una cuenta de dueño con saldo cero")]
    [EndpointDescription("Sin body. Dueño de la cuenta o administrador habilitado. Ejemplo: POST /api/v1/cuentas/1/cerrar. Devuelve estado CERRADA y fechaCierre UTC; repetir devuelve la misma fecha sin otra auditoría. Cuenta ajena/inexistente: 404. Saldo distinto de cero o cuenta PLATAFORMA/IMPUESTOS: 409. ID no positivo: 400.")]
    [ProducesResponseType<CuentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CuentaDto>> Cerrar(int id, CancellationToken ct) =>
        FromResult(await cerrar.HandleAsync(new CerrarCuentaCommand(id), ct));
}
