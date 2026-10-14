using System.Net;
using System.Text.Json;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.AI;

internal static class OllamaHttp
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal const int MaxRespuestaBytes = 512 * 1024;

    internal static Error ErrorProveedor(HttpStatusCode? status = null) => status == HttpStatusCode.TooManyRequests
        ? new("ia.cuota", "El proveedor alcanzó su límite de uso. Intenta más tarde.", ErrorType.RateLimit)
        : new("ia.proveedor", "El servicio de IA no está disponible o devolvió una respuesta inválida.", ErrorType.BusinessRule);

    internal static async Task<JsonDocument> LeerAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > MaxRespuestaBytes) throw new JsonException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bloque = new byte[8192];
        int leidos;
        while ((leidos = await stream.ReadAsync(bloque, ct)) > 0)
        {
            if (buffer.Length + leidos > MaxRespuestaBytes) throw new JsonException();
            buffer.Write(bloque, 0, leidos);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
}
