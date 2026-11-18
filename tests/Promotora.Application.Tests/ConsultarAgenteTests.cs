using Promotora.Application.AI.Consultas;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Domain.Shared;

namespace Promotora.Application.Tests;

public sealed class ConsultarAgenteTests
{
    [Fact]
    public async Task CalificacionUsaHerramientaSeleccionadaYCitaResultadoReal()
    {
        var puertos = new Puertos { IdSeleccionado = 12, FuenteCalificacion = new("calificacion-presentador:12", "Calificación pública", null, null, null, "/api/v1/presentadores/12/calificacion", "El presentador 12 tiene una calificación promedio de 4.50 sobre 5, basada en 2 reseñas aprobadas.") };
        var handler = new ConsultarAgenteHandler(new(puertos, puertos, new()), new(), puertos, [], puertos, puertos);
        var result = await handler.HandleAsync(new("¿Cuál es la calificación del presentador con ID 12?"), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal("OK", result.Value.Estado);
        Assert.Equal(12, puertos.IdConsultado);
        Assert.Equal(ConsultarAgenteHandler.HerramientaCalificacion, Assert.Single(result.Value.HerramientasUtilizadas!));
        Assert.Equal("calificacion-presentador:12", Assert.Single(result.Value.Fuentes).Id);
        Assert.Equal(8, result.Value.TokensEntrada);
        Assert.Equal(12, result.Value.TokensSalida);
    }

    [Fact]
    public async Task RechazaIdInventadoPorElModeloSinConsultarDatos()
    {
        var puertos = new Puertos { IdSeleccionado = 99 };
        var handler = new ConsultarAgenteHandler(new(puertos, puertos, new()), new(), puertos, [], puertos, puertos);
        var result = await handler.HandleAsync(new("¿Cuál es la calificación del presentador 12?"), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Null(puertos.IdConsultado);
    }

    [Fact]
    public async Task PresentadorInexistenteNoInventaCalificacion()
    {
        var puertos = new Puertos { IdSeleccionado = 12 };
        var handler = new ConsultarAgenteHandler(new(puertos, puertos, new()), new(), puertos, [], puertos, puertos);
        var result = await handler.HandleAsync(new("¿Cuál es la calificación del presentador 12?"), TestContext.Current.CancellationToken);
        Assert.Equal("SIN_CONTEXTO", result.Value.Estado);
        Assert.Contains("No encontré", result.Value.Respuesta);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(ConsultarAgenteHandler.HerramientaCalificacion, Assert.Single(result.Value.HerramientasUtilizadas!));
    }
    [Fact]
    public async Task SinContextoNoInvocaLlmYConContextoDevuelveCitaVerificada()
    {
        var puertos = new Puertos();
        var handler = new ConsultarAgenteHandler(new(puertos, puertos, new()), new(), puertos, [], puertos, puertos);
        var vacio = await handler.HandleAsync(new("¿Cómo se entra al evento?"), TestContext.Current.CancellationToken);
        Assert.Equal("SIN_CONTEXTO", vacio.Value.Estado); Assert.Equal(0, puertos.LlamadasLlm);
        puertos.HayContexto = true;
        var respuesta = await handler.HandleAsync(new("¿Cómo se entra al evento?", vacio.Value.SessionId), TestContext.Current.CancellationToken);
        Assert.Equal("OK", respuesta.Value.Estado); Assert.Equal(vacio.Value.SessionId, respuesta.Value.SessionId);
        Assert.Equal(1, puertos.LlamadasLlm); Assert.Equal(7, Assert.Single(respuesta.Value.Fuentes).ChunkId);
    }
    private sealed class Puertos : ILlmPort, IEmbeddingPort, IVectorSearchPort, ISelectorHerramientaPort, ICalificacionPublicaPort
    {
        public int? IdSeleccionado { get; set; }
        public int? IdConsultado { get; private set; }
        public FuenteContexto? FuenteCalificacion { get; set; }
        public Task<Result<SeleccionHerramienta>> SeleccionarAsync(string pregunta, CancellationToken ct) => Task.FromResult(Result<SeleccionHerramienta>.Success(new(IdSeleccionado, 3, 4)));
        public Task<FuenteContexto?> ConsultarAsync(int presentadorId, CancellationToken ct) { IdConsultado = presentadorId; return Task.FromResult(FuenteCalificacion); }
        public bool HayContexto { get; set; }
        public int LlamadasLlm { get; private set; }
        public Task<Result<RespuestaLlm>> ResponderAsync(SolicitudLlm solicitud, CancellationToken ct)
        {
            LlamadasLlm++;
            string texto = FuenteCalificacion is null ? """{"extractos":[{"fuente":"rag:7","texto":"Presenta tu boleto con QR para validación."}]}"""
                : System.Text.Json.JsonSerializer.Serialize(new { extractos = new[] { new { fuente = FuenteCalificacion.Id, texto = FuenteCalificacion.Contenido } } });
            return Task.FromResult(Result<RespuestaLlm>.Success(new(texto, "fake", 5, 8)));
        }
        public Task<Result<EmbeddingsGenerados>> GenerarAsync(IReadOnlyList<string> textos, FinalidadEmbedding finalidad, CancellationToken ct)
        { float[] vector = new float[1536]; vector[0] = 1; return Task.FromResult(Result<EmbeddingsGenerados>.Success(new([vector], "fake", 1))); }
        public Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(float[] embedding, int limite, double similitudMinima, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FragmentoRecuperado>>(HayContexto ? [new(7, 1, "Guía", 2, "Presenta tu boleto con QR para validación.", 1)] : []);
    }
}
