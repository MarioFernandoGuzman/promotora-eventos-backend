using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Promotora.Application.AI.Consultas;
using Promotora.Application.AI.Ports;
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

    [HttpPost("~/api/v1/ia/consultas/stream")]
    [AllowAnonymous]
    [EnableRateLimiting(IdentityHttpProtection.AgenteIa)]
    [RequestSizeLimit(8192)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EndpointSummary("Consultar a José con respuesta progresiva y fuentes verificadas")]
    public async Task ConsultarStreaming(ConsultarAgenteCommand request, CancellationToken ct)
    {
        var handler = HttpContext.RequestServices.GetService<ICommandHandler<ConsultarAgenteCommand, ConsultaIaDto>>();
        var progreso = HttpContext.RequestServices.GetService<ProgresoStreamingIa>();
        if (handler is null || progreso is null) { Response.StatusCode = 422; return; }
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        async Task EventoAsync(string tipo, object datos, CancellationToken token)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(datos, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            await Response.WriteAsync($"event: {tipo}\ndata: {json}\n\n", token);
            await Response.Body.FlushAsync(token);
        }
        progreso.EnviarAsync = async (texto, token) =>
        {
            foreach (string palabra in System.Text.RegularExpressions.Regex.Matches(texto, @"\s*\S+\s*", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Select(m => m.Value))
            {
                await EventoAsync("delta", new { texto = palabra }, token);
                await Task.Delay(20, token);
            }
        };
        var result = await handler.HandleAsync(request, ct);
        if (result.IsFailure) await EventoAsync("error", new { detail = result.Errors[0].Message }, ct);
        else await EventoAsync("final", result.Value, ct);
    }
}
