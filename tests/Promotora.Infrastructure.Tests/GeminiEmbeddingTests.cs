using System.Net;
using System.Text.Json;
using Promotora.Application.AI.Ports;
using Promotora.Infrastructure.AI;

namespace Promotora.Infrastructure.Tests;

public sealed class GeminiEmbeddingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static GeminiEmbeddingOptions Options => new() { ApiKey = "gemini-simulada-no-real" };

    [Theory]
    [InlineData(FinalidadEmbedding.Documento, "title: none | text: ")]
    [InlineData(FinalidadEmbedding.Consulta, "task: question answering | query: ")]
    public async Task EndpointEstandar1536PorTextoConClaveSeparadaYOrdenEstable(FinalidadEmbedding finalidad, string prefijo)
    {
        int llamadas = 0;
        string[] textos = ["FAQ", "Reglamento"];
        using var http = new HttpClient(new Stub(async request =>
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent", request.RequestUri!.ToString());
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("gemini-simulada-no-real", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(Ct));
            Assert.Equal(1536, body.RootElement.GetProperty("outputDimensionality").GetInt32());
            Assert.Equal(prefijo + textos[llamadas], body.RootElement.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.False(body.RootElement.TryGetProperty("taskType", out _));
            return Json(Vector(1536, llamadas++));
        }));
        var result = await new GeminiEmbeddingAdapter(http, Options).GenerarAsync(textos, finalidad, Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, llamadas);
        Assert.Equal(1, result.Value.Vectores[0][0]);
        Assert.Equal(1, result.Value.Vectores[1][1]);
        Assert.Null(result.Value.TokensEntrada);
        Assert.DoesNotContain("gemini-simulada", Options.ToString());
    }

    [Theory]
    [InlineData(1024, false)]
    [InlineData(1536, true)]
    [InlineData(1536, false)]
    public async Task RechazaDimensionInvalidaYCeroSinDevolverLoteParcial(int dimension, bool cero)
    {
        int llamadas = 0;
        using var http = new HttpClient(new Stub(_ =>
        {
            llamadas++;
            if (llamadas == 1) return Task.FromResult(Json(Vector(1536, 0)));
            return Task.FromResult(cero || dimension != 1536 ? Json(new float[dimension]) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"embedding\":{\"values\":[\"NaN\"]}}") });
        }));
        Assert.True((await new GeminiEmbeddingAdapter(http, Options).GenerarAsync(["Uno", "Dos"], FinalidadEmbedding.Documento, Ct)).IsFailure);
        Assert.Equal(2, llamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "ia.cuota")]
    [InlineData(HttpStatusCode.Unauthorized, "ia.proveedor")]
    [InlineData(HttpStatusCode.BadGateway, "ia.proveedor")]
    public async Task CuotaYFallosNoReintentanNiExponenErrorDelProveedor(HttpStatusCode status, string code)
    {
        int llamadas = 0;
        using var http = new HttpClient(new Stub(_ =>
        {
            llamadas++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("cuerpo-confidencial") });
        }));
        var result = await new GeminiEmbeddingAdapter(http, Options).GenerarAsync(["Uno", "Dos"], FinalidadEmbedding.Documento, Ct);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
        Assert.DoesNotContain("cuerpo-confidencial", result.Errors[0].Message);
        Assert.Equal(1, llamadas);
    }

    [Fact]
    public async Task ValidaTodoElLoteAntesDeConsumirCuota()
    {
        using var http = new HttpClient(new Stub(_ => throw new InvalidOperationException("No llamar")));
        var adapter = new GeminiEmbeddingAdapter(http, Options);
        Assert.True((await adapter.GenerarAsync([], FinalidadEmbedding.Documento, Ct)).IsFailure);
        Assert.True((await adapter.GenerarAsync(Enumerable.Repeat("Texto", 17).ToArray(), FinalidadEmbedding.Documento, Ct)).IsFailure);
        Assert.True((await adapter.GenerarAsync(["Válido", new string('界', 2001)], FinalidadEmbedding.Documento, Ct)).IsFailure);
        Assert.True((await adapter.GenerarAsync(["Texto"], (FinalidadEmbedding)99, Ct)).IsFailure);
    }

    [Fact]
    public async Task CancelacionNoDevuelveVectoresParciales()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var http = new HttpClient(new Stub(async _ =>
        {
            await stop.CancelAsync();
            throw new OperationCanceledException(stop.Token);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GeminiEmbeddingAdapter(http, Options).GenerarAsync(["Texto"], FinalidadEmbedding.Consulta, stop.Token));
    }

    [Fact]
    public void ConfiguracionRequiereClaveExternaSinFiltrarla()
    {
        Assert.Throws<InvalidOperationException>(() => new GeminiEmbeddingOptions().Validar());
        Options.Validar();
    }

    [Fact]
    public async Task CuentaTokensInformadosSinSolicitudAdicional()
    {
        int llamadas = 0;
        using var http = new HttpClient(new Stub(_ =>
        {
            llamadas++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { embedding = new { values = Vector(1536, 0) }, usageMetadata = new { promptTokenCount = 7 } }))
            });
        }));
        var result = await new GeminiEmbeddingAdapter(http, Options).GenerarAsync(["Uno", "Dos"], FinalidadEmbedding.Documento, Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(14, result.Value.TokensEntrada);
        Assert.Equal(2, llamadas);
    }

    private static float[] Vector(int dimensiones, int posicion) { float[] vector = new float[dimensiones]; vector[posicion] = 1; return vector; }
    private static HttpResponseMessage Json(float[] vector) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { embedding = new { values = vector } })) };
    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
}
