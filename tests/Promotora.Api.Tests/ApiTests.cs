using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Domain.Shared;
using Promotora.WebApi.Controllers;

namespace Promotora.Api.Tests;

public sealed class ApiTests
{
    [Fact]
    public async Task SwaggerUiYDocumentoV1DocumentanRegistroYAltas()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var ui = await client.GetAsync("/swagger/index.html", ct);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        _ = factory.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        Assert.Equal("v1", document.GetProperty("info").GetProperty("version").GetString());
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/auth/registro", out _));
        Assert.False(document.GetProperty("paths").GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(document.GetProperty("paths").GetProperty("/api/v1/usuarios").GetProperty("get").TryGetProperty("security", out _));
        Assert.True(document.GetProperty("components").GetProperty("schemas").GetProperty("RegistroRequest").TryGetProperty("example", out _));
    }

    [Fact]
    public async Task SwaggerNoSeExponeEnProduction()
    {
        await using var factory = new ApiFactory("Production");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LivenessEsIndependienteYReadinessRequiereConexion()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", ct)).StatusCode);
        var ready = await client.GetAsync("/health/ready", ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task CorrelationIdValidoSeConservaYHeaderInvalidoSeReemplaza()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid().ToString("D");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/no-existe");
        request.Headers.Add("X-Correlation-ID", id);
        var response = await client.SendAsync(request, ct);
        Assert.Equal(id, Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal(id, problem.GetProperty("traceId").GetString());
        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        invalid.Headers.Add("X-Correlation-ID", "texto-no-confiable");
        var replacement = await client.SendAsync(invalid, ct);
        Assert.True(Guid.TryParse(Assert.Single(replacement.Headers.GetValues("X-Correlation-ID")), out _));
    }

    [Fact]
    public async Task ValidacionYExcepcionesUsanProblemDetailsSinDetallesPrivados()
    {
        await using var factory = new ApiFactory();
        await using var withControllers = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(PruebasController).Assembly)));
        using var client = withControllers.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var invalid = await client.PostAsJsonAsync("/api/v1/pruebas", new { nombre = "" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        var body = await invalid.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(body.TryGetProperty("errors", out _));
        Assert.True(body.TryGetProperty("traceId", out _));
        var failure = await client.GetAsync("/api/v1/pruebas/error", ct);
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.DoesNotContain("detalle privado de prueba", await failure.Content.ReadAsStringAsync(ct));
        var success = await client.PostAsJsonAsync("/api/v1/pruebas", new { nombre = "Prueba" }, ct);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var successBody = await success.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Prueba", successBody.GetProperty("nombre").GetString());
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.BusinessRule, 422)]
    [InlineData(ErrorType.RateLimit, 429)]
    public void ErroresEsperadosSeMapeanAlCodigoHttp(ErrorType type, int expected)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext { TraceIdentifier = "prueba" };
        var problem = Promotora.WebApi.Errors.ProblemDetailsMapper.Create([new Error("prueba", "Mensaje", type)], context);
        Assert.Equal(expected, problem.Status);
        Assert.Equal("prueba", problem.Extensions["traceId"]);
    }
}

// Solo se registra explícitamente en el host de pruebas; no existe en la API.
public sealed class PruebasController : ApiControllerBase
{
    [HttpPost]
    public ActionResult<PruebaDto> Post(PruebaDto dto) => FromResult(Result<PruebaDto>.Success(dto));

    [HttpGet("error")]
    public IActionResult Error() => throw new InvalidOperationException("detalle privado de prueba");
}

public sealed record PruebaDto([Required] string Nombre);
