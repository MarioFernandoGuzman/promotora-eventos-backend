using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Images;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    [Fact]
    public async Task FalloAlAsignarUrlRevierteAuditoriaYEliminaSoloArchivoNuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var original = new IdentityApiFactory(fixture.ConnectionString);
        await using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IImagenResourceRepository, RejectImagenResourceRepository>()));
        using var owner = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        await ProblemAsync(await UploadAsync(owner, $"/api/v1/recintos/{recinto}/imagen-plano", ImagenFixture.Imagen(), ct), 409, ct);
        Assert.Empty(Directory.GetFiles(original.ImagesRoot));
        await using var db = fixture.CreateContext();
        Assert.Null((await db.Recintos.AsNoTracking().SingleAsync(x => x.Id == recinto, ct)).ImagenPlanoUrl);
        Assert.Equal(0, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "recintos" && x.EntidadId == recinto.ToString() && x.Accion == "UPDATE", ct));
    }
    private sealed class RejectImagenResourceRepository : IImagenResourceRepository
    {
        public Task<bool> AssignAsync(DestinoImagen destino, int id, string url, string? estadoEsperado, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> PublicReferenceAsync(string url, CancellationToken ct) => Task.FromResult(false);
    }
}

