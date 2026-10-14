using System.Diagnostics;
using System.Text.RegularExpressions;
using Promotora.Application.AI.Rag;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Consultas;

public sealed record ConsultaRegistrada(Guid SessionId, string Consulta, string? Respuesta, IReadOnlyList<FuenteCitadaDto> Fuentes,
    string Estado, string? Modelo, int? TokensEntrada, int? TokensSalida, int TiempoRespuestaMs, DateTime FechaHora);
public interface IConsultaIaLogPort
{
    Task GuardarAsync(ConsultaRegistrada consulta, CancellationToken ct);
}
public static class ConsultaPrivacidad
{
    public static bool ContieneCredenciales(string texto) => Regex.IsMatch(texto,
        @"(?i)(contrase[nñ]a|password|api[_\s-]?key|bearer|jwt|refresh.?token|clave\s*[:=]|\b[a-z0-9_./+=-]{24,}\b)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string Redactar(string texto) => ContieneCredenciales(texto) ? "[Contenido omitido por seguridad]" : texto;
}
// Decorador del mismo caso de uso, no invoca casos de uso de otros módulos.
public sealed class RegistrarConsultaHandler(ConsultarAgenteHandler inner, IConsultaIaLogPort logs, IClock reloj)
    : ICommandHandler<ConsultarAgenteCommand, ConsultaIaDto>
{
    public async Task<Result<ConsultaIaDto>> HandleAsync(ConsultarAgenteCommand request, CancellationToken ct)
    {
        var normalizada = request with { SessionId = request.SessionId ?? Guid.NewGuid() };
        long inicio = Stopwatch.GetTimestamp();
        Result<ConsultaIaDto> resultado;
        try { resultado = await inner.HandleAsync(request, ct); }
        catch
        {
            using var presupuesto = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await logs.GuardarAsync(new(normalizada.SessionId!.Value, "[Consulta interrumpida]", null, [], "ERROR", null, null, null,
                Duracion(inicio), reloj.UtcNow), presupuesto.Token);
            throw;
        }
        var dto = resultado.IsSuccess ? resultado.Value : null;
        await logs.GuardarAsync(new(dto?.SessionId ?? normalizada.SessionId!.Value,
            dto?.Estado is "RECHAZADA" or "ERROR" || resultado.IsFailure ? "[Consulta omitida por seguridad]" : ConsultaPrivacidad.Redactar(request.Pregunta),
            dto is null ? null : ConsultaPrivacidad.Redactar(dto.Respuesta), dto?.Fuentes ?? [], dto?.Estado ?? "ERROR", dto?.Modelo, dto?.TokensEntrada, dto?.TokensSalida,
            Duracion(inicio), reloj.UtcNow), ct);
        return resultado;
    }
    private static int Duracion(long inicio) => (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
}
