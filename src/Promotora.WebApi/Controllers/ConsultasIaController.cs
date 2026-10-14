using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Promotora.Application.AI.Consultas;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

public sealed class ConsultasIaController : ApiControllerBase
{
    [HttpPost("~/api/v1/ia/consultas")]
    [AllowAnonymous]
    [EnableRateLimiting(IdentityHttpProtection.AgenteIa)]
    [RequestSizeLimit(8192)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Preguntar a José sobre información pública con fuentes")]
    [EndpointDescription("Body: {\"pregunta\":\"¿Cómo se valida la entrada?\",\"sessionId\":null}. Pregunta 1–1000 caracteres, sessionId UUID opcional. Reutilizar sessionId agrupa logs; no recupera conversaciones ni datos personales. Estado OK incluye citas literales; SIN_CONTEXTO no inventa respuesta. IA deshabilitada/proveedor no disponible: 422; cuota/rate limit: 429. No realiza compras ni accede a información privada.")]
    [ProducesResponseType<ConsultaIaDto>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(413)]
    [ProducesResponseType<ProblemDetails>(422)]
    [ProducesResponseType<ProblemDetails>(429)]
    public async Task<ActionResult<ConsultaIaDto>> Consultar(ConsultarAgenteCommand request, CancellationToken ct)
    {
        var handler = HttpContext.RequestServices.GetService<ICommandHandler<ConsultarAgenteCommand, ConsultaIaDto>>();
        return handler is null ? FromResult(Result<ConsultaIaDto>.Failure(new Error("ia.deshabilitada", "IA no está habilitada.", ErrorType.BusinessRule)))
            : FromResult(await handler.HandleAsync(request, ct));
    }
}
