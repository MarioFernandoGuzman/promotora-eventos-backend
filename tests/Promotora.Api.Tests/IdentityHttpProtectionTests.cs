using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Api.Tests;

public sealed class IdentityHttpProtectionTests
{
    [Fact]
    public async Task LimitesIndependientesDevuelven429ConRetryAfterYProblemDetails()
    {
        await using var factory = new ApiFactory(settings: new Dictionary<string, string?>
        {
            ["RateLimiting:LoginPermitLimit"] = "2",
            ["RateLimiting:RegistroPermitLimit"] = "2"
        });
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ICommandHandler<LoginCommand, TokenDto>>(new Stub<LoginCommand, TokenDto>());
            services.AddSingleton<ICommandHandler<RefreshCommand, TokenDto>>(new Stub<RefreshCommand, TokenDto>());
            services.AddSingleton<ICommandHandler<LogoutCommand, Unit>>(new Stub<LogoutCommand, Unit>());
            services.AddSingleton<ICommandHandler<CrearUsuarioCommand, UsuarioDto>>(new Stub<CrearUsuarioCommand, UsuarioDto>());
        }));
        using var client = configured.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        foreach (string path in new[] { "/api/v1/auth/login", "/api/v1/auth/registro" })
        {
            var body = new { email = "test@example.test", password = Guid.NewGuid().ToString("N"), nombre = "Nombre", apellido = "Apellido" };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, body, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, body, ct)).StatusCode);
            var response = await client.PostAsJsonAsync(path, body, ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(429, problem.GetProperty("status").GetInt32());
            Assert.True(problem.TryGetProperty("traceId", out _));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", ct)).StatusCode);
    }

    [Fact]
    public async Task CorsSoloPermiteOrigenConfiguradoYSinCredenciales()
    {
        await using var factory = new ApiFactory(settings: new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://frontend.example.test" });
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        foreach (string origin in new[] { "https://frontend.example.test", "https://otro.example.test" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
            var response = await client.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            bool allowed = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values);
            Assert.Equal(origin == "https://frontend.example.test", allowed);
            if (allowed)
            {
                Assert.Equal(origin, Assert.Single(values!));
            }

            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
    }

    private sealed class Stub<TCommand, TResponse> : ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> HandleAsync(TCommand request, CancellationToken ct) => Task.FromResult(
            Result<TResponse>.Failure(new Error("test.unauthorized", "Solo prueba de transporte.", ErrorType.Unauthorized)));
    }
}
