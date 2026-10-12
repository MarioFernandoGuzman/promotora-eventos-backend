using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Ports;

public sealed record SolicitudLlm(string Pregunta, string ContextoPublico, bool Presentarse = false);
public sealed record RespuestaLlm(string Texto, string Modelo, int TokensEntrada, int TokensSalida);
public sealed record EmbeddingsGenerados(IReadOnlyList<float[]> Vectores, string Modelo, int TokensEntrada);
public sealed record FragmentoRecuperado(long ChunkId, int DocumentoId, string Documento, int? Pagina, string Contenido, double Similitud);

public interface ILlmPort
{
    Task<Result<RespuestaLlm>> ResponderAsync(SolicitudLlm solicitud, CancellationToken ct);
}

public interface IEmbeddingPort
{
    Task<Result<EmbeddingsGenerados>> GenerarAsync(IReadOnlyList<string> textos, CancellationToken ct);
}

public interface IVectorSearchPort
{
    Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(float[] embedding, int limite, double similitudMinima, CancellationToken ct);
}
