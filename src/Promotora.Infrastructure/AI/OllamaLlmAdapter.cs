using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Promotora.Application.AI;
using Promotora.Application.AI.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.AI;

public sealed class OllamaLlmAdapter(HttpClient http, OllamaOptions options) : ILlmPort
{
    public async Task<Result<RespuestaLlm>> ResponderAsync(SolicitudLlm solicitud, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Pregunta) || solicitud.Pregunta.Length > PoliticaAsistente.MaxPreguntaCaracteres
            || solicitud.ContextoPublico.Length > PoliticaAsistente.MaxContextoCaracteres)
            return Result<RespuestaLlm>.Failure(new Error("ia.longitud", "Pregunta o contexto exceden los límites de IA.", ErrorType.Validation));

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://ollama.com/api/chat");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Clave);
        request.Content = JsonContent.Create(new
        {
            model = options.ChatModel,
            stream = false,
            think = "low",
            options = new { num_predict = options.MaxTokensSalida, temperature = 0.2 },
            messages = new[]
            {
                new { role = "system", content = PoliticaAsistente.Instrucciones },
                new { role = "user", content = JsonSerializer.Serialize(solicitud, OllamaHttp.Json) }
            }
        });
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSegundos));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor(response.StatusCode));
            using var json = await OllamaHttp.LeerAsync(response, deadline.Token);
            var root = json.RootElement;
            string? texto = root.GetProperty("message").GetProperty("content").GetString();
            int entrada = root.GetProperty("prompt_eval_count").GetInt32();
            int salida = root.GetProperty("eval_count").GetInt32();
            if (root.GetProperty("done").GetBoolean() != true || root.GetProperty("done_reason").GetString() != "stop"
                || string.IsNullOrWhiteSpace(texto) || texto.Length > 8000 || entrada < 0 || salida < 0
                || root.GetProperty("model").GetString() != options.ChatModel)
                return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor());
            return Result<RespuestaLlm>.Success(new(texto, options.ChatModel, entrada, salida));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor()); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return Result<RespuestaLlm>.Failure(OllamaHttp.ErrorProveedor()); }
    }
}
