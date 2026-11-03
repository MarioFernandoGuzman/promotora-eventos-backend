using Promotora.Application.AI.Consultas;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Domain.Shared;

namespace Promotora.Application.Tests;

public sealed class ConsultarAgenteTests
{
    [Fact]
    public async Task SinContextoNoInvocaLlmYConContextoDevuelveCitaVerificada()
    {
        var puertos = new Puertos();
        var handler = new ConsultarAgenteHandler(new(puertos, puertos, new()), new(), puertos, []);
        var vacio = await handler.HandleAsync(new("¿Cómo se entra al evento?"), TestContext.Current.CancellationToken);
        Assert.Equal("SIN_CONTEXTO", vacio.Value.Estado); Assert.Equal(0, puertos.LlamadasLlm);
        puertos.HayContexto = true;
        var respuesta = await handler.HandleAsync(new("¿Cómo se entra al evento?", vacio.Value.SessionId), TestContext.Current.CancellationToken);
        Assert.Equal("OK", respuesta.Value.Estado); Assert.Equal(vacio.Value.SessionId, respuesta.Value.SessionId);
        Assert.Equal(1, puertos.LlamadasLlm); Assert.Equal(7, Assert.Single(respuesta.Value.Fuentes).ChunkId);
    }
    private sealed class Puertos : ILlmPort, IEmbeddingPort, IVectorSearchPort
    {
        public bool HayContexto { get; set; }
        public int LlamadasLlm { get; private set; }
        public Task<Result<RespuestaLlm>> ResponderAsync(SolicitudLlm solicitud, CancellationToken ct)
        { LlamadasLlm++; return Task.FromResult(Result<RespuestaLlm>.Success(new("""{"extractos":[{"fuente":"rag:7","texto":"Presenta tu boleto con QR para validación."}]}""", "fake", 5, 8))); }
        public Task<Result<EmbeddingsGenerados>> GenerarAsync(IReadOnlyList<string> textos, FinalidadEmbedding finalidad, CancellationToken ct)
        { float[] vector = new float[1536]; vector[0] = 1; return Task.FromResult(Result<EmbeddingsGenerados>.Success(new([vector], "fake", 1))); }
        public Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(float[] embedding, int limite, double similitudMinima, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FragmentoRecuperado>>(HayContexto ? [new(7, 1, "Guía", 2, "Presenta tu boleto con QR para validación.", 1)] : []);
    }
}
