using System.Net;
using System.Text.Json;
using Promotora.Application.AI;
using Promotora.Infrastructure.AI;

namespace Promotora.Infrastructure.Tests;

public sealed class GeminiChatAdapterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static GeminiEmbeddingOptions Options => new() { ApiKey = "clave-simulada-test" };

    [Fact]
    public async Task RespuestaUsaGeminiConContextoDelimitadoYCuentaTokens()
    {
        using var http = new HttpClient(new Stub(async request =>
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent", request.RequestUri!.ToString());
            Assert.Equal("clave-simulada-test", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(Ct));
            var root = body.RootElement;
            Assert.Equal(PoliticaAsistente.Instrucciones, root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("application/json", root.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
            Assert.False(root.TryGetProperty("tools", out _));
            return Json(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = "{\"extractos\":[]}" } } } } }, usageMetadata = new { promptTokenCount = 12, candidatesTokenCount = 4, thoughtsTokenCount = 2 } });
        }));
        var result = await new GeminiChatAdapter(http, Options).ResponderAsync(new("Eventos", "[]"), Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(GeminiChatAdapter.Modelo, result.Value.Modelo);
        Assert.Equal(12, result.Value.TokensEntrada);
        Assert.Equal(6, result.Value.TokensSalida);
    }

    [Fact]
    public async Task SelectorSolicitaHerramientaYLeeLlamada()
    {
        using var http = new HttpClient(new Stub(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(Ct));
            Assert.Equal("consultar_calificacion_presentador", body.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations")[0].GetProperty("name").GetString());
            return Json(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { functionCall = new { name = "consultar_calificacion_presentador", args = new { presentador_id = 12 } } } } } } }, usageMetadata = new { promptTokenCount = 10, candidatesTokenCount = 4 } });
        }));
        var result = await new GeminiChatAdapter(http, Options).SeleccionarAsync("Calificación del presentador 12", Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.PresentadorId);
        Assert.Equal(4, result.Value.TokensSalida);
    }

    [Fact]
    public async Task SelectorRechazaHerramientaDesconocida()
    {
        using var http = new HttpClient(new Stub(_ => Task.FromResult(Json(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { functionCall = new { name = "otra", args = new { presentador_id = 12 } } } } } } }, usageMetadata = new { promptTokenCount = 10 } }))));
        var result = await new GeminiChatAdapter(http, Options).SeleccionarAsync("Calificación del presentador 12", Ct);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RespuestaTruncadaSeRechaza()
    {
        using var http = new HttpClient(new Stub(_ => Task.FromResult(Json(new { candidates = new[] { new { finishReason = "MAX_TOKENS", content = new { parts = new[] { new { text = "parcial" } } } } }, usageMetadata = new { promptTokenCount = 10 } }))));
        Assert.True((await new GeminiChatAdapter(http, Options).ResponderAsync(new("Eventos", "[]"), Ct)).IsFailure);
    }

    [Fact]
    public async Task ErrorDeCuotaNoExponeRespuestaDelProveedor()
    {
        using var http = new HttpClient(new Stub(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("dato privado") })));
        var result = await new GeminiChatAdapter(http, Options).ResponderAsync(new("Eventos", "[]"), Ct);
        Assert.Equal("ia.cuota", Assert.Single(result.Errors).Code);
        Assert.DoesNotContain("dato privado", result.Errors[0].Message);
    }

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };
    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
}
