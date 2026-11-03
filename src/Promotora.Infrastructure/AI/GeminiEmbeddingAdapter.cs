using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Promotora.Application.AI;
using Promotora.Application.AI.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.AI;

public sealed class GeminiEmbeddingAdapter(HttpClient http, GeminiEmbeddingOptions options) : IEmbeddingPort
{
    public async Task<Result<EmbeddingsGenerados>> GenerarAsync(IReadOnlyList<string> textos, FinalidadEmbedding finalidad, CancellationToken ct)
    {
        if (!Enum.IsDefined(finalidad) || textos.Count is < 1 or > 16
            || textos.Any(t => string.IsNullOrWhiteSpace(t) || Encoding.UTF8.GetByteCount(t) > 6000))
            return Result<EmbeddingsGenerados>.Failure(new Error("ia.lote", "El lote requiere de 1 a 16 textos de hasta 6000 bytes UTF-8 y una finalidad válida.", ErrorType.Validation));

        // El endpoint estándar produce un vector por texto. No usar Batch API diferida (sin Free Tier).
        var vectores = new List<float[]>();
        int tokensEntrada = 0;
        bool consumoCompleto = true;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSegundos));
            foreach (string texto in textos)
            {
                string contenido = finalidad == FinalidadEmbedding.Consulta
                    ? $"task: question answering | query: {texto}"
                    : $"title: none | text: {texto}";
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiEmbeddingOptions.Modelo}:embedContent");
                request.Headers.Add("x-goog-api-key", options.Clave);
                request.Content = JsonContent.Create(new
                {
                    model = $"models/{GeminiEmbeddingOptions.Modelo}",
                    content = new { parts = new[] { new { text = contenido } } },
                    outputDimensionality = PoliticaAsistente.Dimensiones
                });
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if (!response.IsSuccessStatusCode) return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor(response.StatusCode));
                using var json = await OllamaHttp.LeerAsync(response, deadline.Token);
                float[] vector = json.RootElement.GetProperty("embedding").GetProperty("values").Deserialize<float[]>(OllamaHttp.Json) ?? [];
                if (vector.Length != PoliticaAsistente.Dimensiones || vector.Any(n => !float.IsFinite(n)) || vector.All(n => n == 0))
                    return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor());
                if (json.RootElement.TryGetProperty("usageMetadata", out var consumo)
                    && consumo.TryGetProperty("promptTokenCount", out var contador))
                {
                    int tokens = contador.GetInt32();
                    if (tokens < 0) return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor());
                    tokensEntrada = checked(tokensEntrada + tokens);
                }
                else consumoCompleto = false;
                vectores.Add(vector);
            }
            // Sin metadata en alguna respuesta, no presentar una suma parcial como consumo total.
            return Result<EmbeddingsGenerados>.Success(new(vectores, GeminiEmbeddingOptions.Modelo, consumoCompleto ? tokensEntrada : null));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor()); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor()); }
    }
}
