using System.Text.Json;
using System.Text.RegularExpressions;
using Promotora.Application.AI.Consultas;
using Promotora.Application.AI.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Rag;

public sealed record FuenteContexto(string Id, string Documento, int? Pagina, long? ChunkId, int? DocumentoId, string? Url, string Contenido);
public sealed record FuenteCitadaDto(string Id, string Documento, int? Pagina, long? ChunkId, int? DocumentoId, string? Url, string Extracto);
public sealed record ContenidoFundamentado(string Respuesta, IReadOnlyList<FuenteCitadaDto> Fuentes);
public sealed record ContextoRag(string Json, IReadOnlyList<FuenteContexto> Fuentes);
public sealed record RagOptions(int TopK = 3, double SimilitudMinima = 0.65)
{
    public void Validar()
    {
        if (TopK is < 1 or > 5 || !double.IsFinite(SimilitudMinima) || SimilitudMinima is < 0 or > 1)
            throw new InvalidOperationException("RAG requiere topK 1–5 y similitud 0–1.");
    }
}
public sealed class RespuestaFundamentada
{
    public ContextoRag Preparar(IEnumerable<FuenteContexto> fuentes)
    {
        var seleccion = new List<FuenteContexto>();
        foreach (var fuente in fuentes.DistinctBy(f => f.Id))
        {
            if (!SeguridadAgente.ContenidoSeguro(fuente.Contenido) || !SeguridadAgente.ContenidoSeguro(fuente.Documento)) continue;
            var candidata = fuente with { Documento = fuente.Documento[..Math.Min(200, fuente.Documento.Length)] };
            if (JsonSerializer.Serialize(seleccion.Append(candidata)).Length > PoliticaAsistente.MaxContextoCaracteres) continue;
            seleccion.Add(candidata);
            if (seleccion.Count == 5) break;
        }
        return new(JsonSerializer.Serialize(seleccion), seleccion);
    }
    public Result<ContenidoFundamentado> Validar(string salida, ContextoRag contexto, bool presentarse)
    {
        try
        {
            // El modelo selecciona citas y extractos, no redacta hechos sin respaldo.
            // No se muestra texto libre generado: cada afirmación pública es una cita literal.
            using var json = JsonDocument.Parse(salida);
            var extractos = json.RootElement.GetProperty("extractos");
            if (extractos.ValueKind != JsonValueKind.Array || extractos.GetArrayLength() is < 1 or > 3) return Fallo();
            var citas = new List<FuenteCitadaDto>();
            foreach (var item in extractos.EnumerateArray())
            {
                string? id = item.GetProperty("fuente").GetString(), texto = item.GetProperty("texto").GetString();
                var fuente = contexto.Fuentes.SingleOrDefault(f => f.Id == id);
                if (fuente is null || string.IsNullOrWhiteSpace(texto) || texto.Length is < 8 or > 900
                    || !fuente.Contenido.Contains(texto, StringComparison.Ordinal) || !SeguridadAgente.ContenidoSeguro(texto) || ContenidoPeligroso(texto)) return Fallo();
                citas.Add(new(fuente.Id, fuente.Documento, fuente.Pagina, fuente.ChunkId, fuente.DocumentoId, fuente.Url, texto));
            }
            string saludo = presentarse ? "Hola, soy José, asistente de información pública de la promotora.\n\n" : "";
            string respuesta = saludo + string.Join("\n\n", citas.Select(c => $"{c.Extracto} [{c.Id}]"));
            return Result<ContenidoFundamentado>.Success(new(respuesta, citas));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { return Fallo(); }
    }
    private static bool ContenidoPeligroso(string texto) => Regex.IsMatch(texto,
        @"(?i)(ignora.{0,30}instrucciones|system\s*prompt|api[_\s-]?key|bearer\s+[a-z0-9]|<\s*script|contrase[nñ]a\s*[:=]|https?://)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static Result<ContenidoFundamentado> Fallo() => Result<ContenidoFundamentado>.Failure(new Error("ia.citas", "No hay una respuesta con fuentes verificables suficientes.", ErrorType.BusinessRule));
}
public sealed class RecuperarContexto(IEmbeddingPort embeddings, IVectorSearchPort busqueda, RagOptions options)
{
    public async Task<Result<IReadOnlyList<FuenteContexto>>> BuscarAsync(string pregunta, CancellationToken ct)
    {
        var vector = await embeddings.GenerarAsync([pregunta], FinalidadEmbedding.Consulta, ct);
        if (vector.IsFailure) return Result<IReadOnlyList<FuenteContexto>>.Failure(vector.Errors);
        if (vector.Value.Vectores.Count != 1) return Result<IReadOnlyList<FuenteContexto>>.Failure(new Error("ia.vector", "No se obtuvo un vector válido.", ErrorType.BusinessRule));
        var chunks = await busqueda.BuscarAsync(vector.Value.Vectores[0], options.TopK, options.SimilitudMinima, ct);
        return Result<IReadOnlyList<FuenteContexto>>.Success(chunks.Select(c => new FuenteContexto($"rag:{c.ChunkId}", c.Documento, c.Pagina, c.ChunkId, c.DocumentoId, null, c.Contenido)).ToArray());
    }
}
