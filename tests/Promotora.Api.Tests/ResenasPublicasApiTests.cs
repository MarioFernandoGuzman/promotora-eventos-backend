using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Promotora.Application.Reviews;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class ResenasPublicasApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task PublicoSoloVeAprobadasDelEventoConPaginacionYErrores()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient(); using var publico = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        var primera = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct);
        string ruta = $"/api/v1/eventos/{primera.EventoId}/resenas";
        var pendiente = (await publico.GetFromJsonAsync<PaginaDto<ResenaDto>>(ruta, Ct))!; Assert.Empty(pendiente.Items);
        using var rechazo = await admin.PatchAsJsonAsync($"/api/v1/resenas/{primera.Id}/moderar", new { estadoNuevo = "RECHAZADA" }, Ct); Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
        var rechazada = (await publico.GetFromJsonAsync<PaginaDto<ResenaDto>>(ruta, Ct))!; Assert.Empty(rechazada.Items);
        var segunda = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct);
        using var aprobacion = await admin.PatchAsJsonAsync($"/api/v1/resenas/{segunda.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.OK, aprobacion.StatusCode);
        ruta = $"/api/v1/eventos/{segunda.EventoId}/resenas";
        var aprobadas = (await publico.GetFromJsonAsync<PaginaDto<ResenaDto>>(ruta + "?pageSize=1", Ct))!;
        Assert.Equal(segunda.Id, Assert.Single(aprobadas.Items).Id); Assert.Equal("APROBADA", aprobadas.Items[0].Estado); Assert.Equal(1, aprobadas.Total);
        var fuera = (await publico.GetFromJsonAsync<PaginaDto<ResenaDto>>(ruta + "?page=2&pageSize=1", Ct))!; Assert.Empty(fuera.Items); Assert.Equal(1, fuera.Total);
        var json = await publico.GetFromJsonAsync<JsonElement>(ruta, Ct); Assert.False(json.GetProperty("items")[0].TryGetProperty("clienteId", out _));
        using var inexistente = await publico.GetAsync("/api/v1/eventos/2147483647/resenas", Ct); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        using var invalida = await publico.GetAsync(ruta + "?pageSize=101", Ct); Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        using var idInvalido = await publico.GetAsync("/api/v1/eventos/0/resenas", Ct); Assert.Equal(HttpStatusCode.BadRequest, idInvalido.StatusCode);
    }
}
