using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Finance;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[Authorize(Roles = "ADMINISTRADOR,DUENO_RECINTO")]
public sealed class CuentasController(ICommandHandler<CerrarCuentaCommand, CuentaDto> cerrar,
    ICommandHandler<LiquidarCuentaCommand, LiquidacionDto> liquidar,
    IQueryHandler<CuentasQuery, PaginaDto<CuentaDto>> listar,
    IQueryHandler<ObtenerCuentaQuery, CuentaDto> obtener,
    IQueryHandler<MovimientosCuentaQuery, PaginaDto<MovimientoCuentaDto>> movimientos) : ApiControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Consultar cuentas propias o todas como administrador")]
    [EndpointDescription("Dueño: solo sus cuentas DUENO, incluso cerradas; filtros ajenos devuelven lista vacía. Administrador: todas. Filtros opcionales duenoId (perfil, no usuario), moneda ISO de tres mayúsculas, tipo DUENO/PLATAFORMA/IMPUESTOS, estado ACTIVA/CERRADA. Orden por ID ascendente. page 1–100000; pageSize 1–100, default 20. Ejemplo: ?tipo=DUENO&moneda=GTQ&page=1&pageSize=20.")]
    [ProducesResponseType<PaginaDto<CuentaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaDto<CuentaDto>>> Listar([FromQuery] CuentasQuery query, CancellationToken ct) =>
        FromResult(await listar.HandleAsync(query, ct));

    [HttpGet("{id:int}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Consultar estado y saldo de una cuenta")]
    [EndpointDescription("Dueño de la cuenta o administrador habilitado. Incluye cuentas cerradas. Cuenta ajena/inexistente: 404; ID no positivo: 400. Ejemplo: GET /api/v1/cuentas/1.")]
    [ProducesResponseType<CuentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CuentaDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new(id), ct));

    [HttpGet("{id:int}/movimientos")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Consultar extracto paginado de movimientos de una cuenta")]
    [EndpointDescription("Dueño de la cuenta o administrador habilitado; ajena/inexistente: 404. Monto conserva signo; liquidación tiene compraId null. Filtros tipo VENTA/COMISION/IMPUESTO/LIQUIDACION/REEMBOLSO, compraId positivo, desde inclusivo y hasta exclusivo en UTC (Z o +00:00), desde < hasta. Orden fecha e ID descendentes. page 1–100000; pageSize 1–100, default 20. Ejemplo: ?tipo=VENTA&desde=2026-01-01T00:00:00Z&hasta=2027-01-01T00:00:00Z&pageSize=20. No expone descripcion interna ni referencias de idempotencia.")]
    [ProducesResponseType<PaginaDto<MovimientoCuentaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaDto<MovimientoCuentaDto>>> Movimientos(int id, [FromQuery] string? tipo = null,
        [FromQuery] int? compraId = null, [FromQuery] DateTimeOffset? desde = null, [FromQuery] DateTimeOffset? hasta = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await movimientos.HandleAsync(new(id, tipo, compraId, desde, hasta, page, pageSize), ct));

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
