using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Ticketing;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed record ValidarBoletoRequest(string CodigoQr);

public sealed class BoletosController(IQueryHandler<MisBoletosQuery, PaginaDto<BoletoClienteDto>> listar,
    ICommandHandler<ValidarBoletoCommand, BoletoValidadoDto> validar) : ApiControllerBase
{
    [Authorize(Policy = IdentityPolicies.ValidarBoletos)]
    [HttpPost("validar")]
    [EndpointSummary("Validar firma y registrar una única entrada VALIDO → USADO")]
    [EndpointDescription("Administrador, dueño del recinto o validador de boletos habilitado. Repetido/anulado/compra no completada: 409; firma inválida: 422; ajeno/inexistente: 404. Guarda fechaUso UTC y validadoPor junto con auditoría.")]
    [ProducesResponseType<BoletoValidadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoletoValidadoDto>> Validar(ValidarBoletoRequest request, CancellationToken ct) =>
        FromResult(await validar.HandleAsync(new ValidarBoletoCommand(request.CodigoQr), ct));

    [Authorize(Policy = IdentityPolicies.Cliente)]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Mis boletos con token QR; incluye VALIDO, USADO y ANULADO")]
    [EndpointDescription("El cliente solo recibe sus boletos. El frontend dibuja el QR con codigoQr; no debe registrarlo en logs. Paginación page/pageSize, máximo 100.")]
    [ProducesResponseType<PaginaDto<BoletoClienteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaDto<BoletoClienteDto>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        FromResult(await listar.HandleAsync(new MisBoletosQuery(page, pageSize), ct));
}
