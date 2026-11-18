using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.AI;

internal sealed class GeminiStreamingAdapter(HttpClient http, GeminiEmbeddingOptions options, ProgresoStreamingIa progreso)
{
    private static readonly Regex Extracto = new(""" "fuente"\s*:\s*(?<fuente>"(?:\\.|[^"\\])*")\s*,\s*"texto"\s*:\s*(?<texto>"(?:\\.|[^"\\])*") """, RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace, TimeSpan.FromMilliseconds(100));

    public async Task<Result<RespuestaLlm>> ResponderAsync<T>(T body, SolicitudLlm solicitud, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiChatAdapter.Modelo}:streamGenerateContent?alt=sse");
        request.Headers.Add("x-goog-api-key", options.Clave);
        request.Content = JsonContent.Create(body);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSegundos));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor(response.StatusCode));
            var fuentes = JsonSerializer.Deserialize<FuenteContexto[]>(solicitud.ContextoPublico) ?? [];
            var fundamento = new RespuestaFundamentada();
            var contexto = fundamento.Preparar(fuentes);
            var texto = new StringBuilder();
            int enviados = 0, entrada = -1, salida = 0, totalBytes = 0;
            string? fin = null;
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (await reader.ReadLineAsync(deadline.Token) is string linea)
            {
                totalBytes = checked(totalBytes + Encoding.UTF8.GetByteCount(linea));
                if (totalBytes > OllamaHttp.MaxRespuestaBytes) throw new JsonException();
                if (!linea.StartsWith("data:", StringComparison.Ordinal)) continue;
                string data = linea[5..].Trim();
                if (data.Length == 0 || data == "[DONE]") continue;
                using var json = JsonDocument.Parse(data);
                var root = json.RootElement;
                if (root.TryGetProperty("usageMetadata", out var uso))
                {
                    if (uso.TryGetProperty("promptTokenCount", out var prompt)) entrada = prompt.GetInt32();
                    int candidatos = uso.TryGetProperty("candidatesTokenCount", out var salidaJson) ? salidaJson.GetInt32() : 0;
                    int pensamientos = uso.TryGetProperty("thoughtsTokenCount", out var pensamientoJson) ? pensamientoJson.GetInt32() : 0;
                    if (candidatos < 0 || pensamientos < 0) throw new JsonException();
                    salida = checked(candidatos + pensamientos);
                }
                if (root.TryGetProperty("candidates", out var candidatosJson) && candidatosJson.GetArrayLength() == 1)
                {
                    var candidato = candidatosJson[0];
                    if (candidato.TryGetProperty("finishReason", out var razon)) fin = razon.GetString();
                    if (candidato.TryGetProperty("content", out var contenido) && contenido.TryGetProperty("parts", out var partes))
                        foreach (var parte in partes.EnumerateArray())
                            if ((!parte.TryGetProperty("thought", out var pensamiento) || !pensamiento.GetBoolean()) && parte.TryGetProperty("text", out var parteTexto)) texto.Append(parteTexto.GetString());
                }
                if (texto.Length > 8000) throw new JsonException();
                var encontrados = Extracto.Matches(texto.ToString());
                while (enviados < encontrados.Count && enviados < 3)
                {
                    var match = encontrados[enviados];
                    string? id = JsonSerializer.Deserialize<string>(match.Groups["fuente"].Value);
                    string? extracto = JsonSerializer.Deserialize<string>(match.Groups["texto"].Value);
                    string candidatoJson = JsonSerializer.Serialize(new { extractos = new[] { new { fuente = id, texto = extracto } } });
                    var validado = fundamento.Validar(candidatoJson, contexto, solicitud.Presentarse && enviados == 0);
                    if (validado.IsFailure) break;
                    string fragmento = (enviados == 0 ? "" : "\n\n") + validado.Value.Respuesta;
                    await progreso.EnviarAsync!(fragmento, deadline.Token);
                    enviados++;
                }
            }
            if (fin != "STOP" || entrada < 0 || texto.Length == 0) return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor());
            if (enviados == 0)
            {
                var final = fundamento.Validar(texto.ToString(), contexto, solicitud.Presentarse);
                if (final.IsSuccess) await progreso.EnviarAsync!(final.Value.Respuesta, deadline.Token);
            }
            return Result<RespuestaLlm>.Success(new(texto.ToString(), GeminiChatAdapter.Modelo, entrada, salida));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor()); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor()); }
    }
}
