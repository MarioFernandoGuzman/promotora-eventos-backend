using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    [Fact]
    public async Task AsociacionesValidanPropiedadDuplicadosRolYBajaDePresentador()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var other = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(admin, await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct), ct);
        await LoginAsync(other, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(await CrearRecintoAsync(owner, ct)), ct);
        var presenter = await PostAsync<PresentadorDto>(admin, "/api/v1/presentadores", new { nombre = "Principal", tipo = "ARTISTA" }, ct);
        string url = $"/api/v1/eventos/{evento.Id}/presentadores";
        var relation = await PostAsync<EventoPresentadorDto>(owner, url, new { presentadorId = presenter.Id, rol = "HEADLINER" }, ct);
        Assert.Equal("HEADLINER", relation.Rol);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new { presentadorId = presenter.Id }, ct), 409, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new { presentadorId = presenter.Id, rol = new string('x', 31) }, ct), 400, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new { presentadorId = int.MaxValue }, ct), 404, ct);
        await ProblemAsync(await other.GetAsync(url, ct), 403, ct);
        await ProblemAsync(await other.DeleteAsync($"{url}/{presenter.Id}", ct), 403, ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{url}/{presenter.Id}", new { rol = "INVITADO" }, ct)).StatusCode);
        Assert.Equal("INVITADO", Assert.Single((await owner.GetFromJsonAsync<PaginaDto<EventoPresentadorDto>>(url, ct))!.Items).Rol);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/presentadores/{presenter.Id}", ct)).StatusCode);
        Assert.False(Assert.Single((await owner.GetFromJsonAsync<PaginaDto<EventoPresentadorDto>>(url, ct))!.Items).Activo);
        await ProblemAsync(await owner.PutAsJsonAsync($"{url}/{presenter.Id}", new { rol = "LOCAL" }, ct), 409, ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"{url}/{presenter.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"{url}/{presenter.Id}", ct)).StatusCode);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new { presentadorId = presenter.Id }, ct), 409, ct);
        await using var db = fixture.CreateContext();
        Assert.False(await db.EventoPresentadores.AnyAsync(x => x.EventoId == evento.Id && x.PresentadorId == presenter.Id, ct));
        Assert.True(await db.Presentadores.AnyAsync(x => x.Id == presenter.Id, ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "evento_presentadores" && x.EntidadId == $"{evento.Id}:{presenter.Id}" && x.Accion == "DELETE", ct));
    }
}
