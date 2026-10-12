using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Finance;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[Authorize(Roles = "ADMINISTRADOR,DUENO_RECINTO")]
public sealed class CuentasController(ICommandHandler<CerrarCuentaCommand, CuentaDto> cerrar,
    ICommandHandler<LiquidarCuentaCommand, LiquidacionDto> liquidar) : ApiControllerBase
{
    public sealed record LiquidarCuentaRequest(decimal Monto);

    [HttpPost("{id:int}/liquidar")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [EndpointSummary("Registrar una liquidación al dueño")]
    [EndpointDescription("Solo administrador habilitado. Body: {\"monto\":100.00}. Header Idempotency-Key obligatorio: 1–128 caracteres ASCII alfanuméricos o . _ : -. Clave sensible a mayúsculas y limitada a la cuenta. Devuelve 200 al crear o repetir el mismo monto; otra cantidad con la misma clave devuelve 409. Solo DUENO ACTIVA con saldo suficiente y conciliado. Débito contable simulado, sin transferencia bancaria. Cuenta inexistente: 404; cuenta cerrada, del sistema o saldo insuficiente: 409; monto inválido/header ausente: 400.")]
    [ProducesResponseType<LiquidacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiquidacionDto>> Liquidar(int id, [FromBody] LiquidarCuentaRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey, CancellationToken ct) =>
        FromResult(await liquidar.HandleAsync(new(id, request.Monto, idempotencyKey), ct));

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
