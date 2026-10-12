using System.Net.Http.Json;
using System.Text.Json;
using Promotora.Application.AI;
using Promotora.Application.AI.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.AI;

public sealed class OllamaEmbeddingAdapter(HttpClient http, OllamaOptions options) : IEmbeddingPort
{
    public async Task<Result<EmbeddingsGenerados>> GenerarAsync(IReadOnlyList<string> textos, CancellationToken ct)
    {
        if (textos.Count is < 1 or > 16 || textos.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 12000))
            return Result<EmbeddingsGenerados>.Failure(new Error("ia.lote", "El lote requiere de 1 a 16 textos de hasta 12000 caracteres.", ErrorType.Validation));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.EmbeddingUrl, "api/embed"))
        {
            Content = JsonContent.Create(new { model = options.EmbeddingModel, input = textos, dimensions = PoliticaAsistente.Dimensiones, truncate = false })
        };
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSegundos));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor(response.StatusCode));
            using var json = await OllamaHttp.LeerAsync(response, deadline.Token);
            var root = json.RootElement;
            float[][] vectores = root.GetProperty("embeddings").Deserialize<float[][]>(OllamaHttp.Json) ?? [];
            int tokens = root.GetProperty("prompt_eval_count").GetInt32();
            if (root.GetProperty("model").GetString() != options.EmbeddingModel || tokens < 0 || vectores.Length != textos.Count
                || vectores.Any(v => v is null || v.Length != PoliticaAsistente.Dimensiones || v.Any(n => !float.IsFinite(n)) || v.All(n => n == 0)))
                return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor());
            return Result<EmbeddingsGenerados>.Success(new(vectores, options.EmbeddingModel, tokens));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor()); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return Result<EmbeddingsGenerados>.Failure(OllamaHttp.ErrorProveedor()); }
    }
}
