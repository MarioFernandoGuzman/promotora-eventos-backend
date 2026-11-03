using System.Net;
using System.Net.Http.Json;
using Promotora.Application.Reviews;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CalificacionPresentadorApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task AgregaSoloAprobadasPorAsociacionSinDuplicarCopresentadores()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient(); using var publico = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        var a = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct, 3);
        var b = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct, 5);
        var c = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct, 1);
        await using var db = postgres.CreateContext(); var principal = new PresentadorRow { Nombre = "Principal", Tipo = "ARTISTA" }; var invitado = new PresentadorRow { Nombre = "Invitado", Tipo = "ARTISTA" }; var vacio = new PresentadorRow { Nombre = "Sin reseñas", Tipo = "ARTISTA" };
        db.Presentadores.AddRange(principal, invitado, vacio); await db.SaveChangesAsync(Ct);
        foreach (var resena in new[] { a, b, c }) db.EventoPresentadores.Add(new EventoPresentadorRow { EventoId = resena.EventoId, PresentadorId = principal.Id });
        db.EventoPresentadores.Add(new EventoPresentadorRow { EventoId = a.EventoId, PresentadorId = invitado.Id }); await db.SaveChangesAsync(Ct);
        foreach (var resena in new[] { a, b })
        { using var aprobada = await admin.PatchAsJsonAsync($"/api/v1/resenas/{resena.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode); }
        var media = (await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{principal.Id}/calificacion", Ct))!; Assert.Equal(4m, media.Promedio); Assert.Equal(2, media.Cantidad);
        var otra = (await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{invitado.Id}/calificacion", Ct))!; Assert.Equal(3m, otra.Promedio); Assert.Equal(1, otra.Cantidad);
        using var rechazada = await admin.PatchAsJsonAsync($"/api/v1/resenas/{c.Id}/moderar", new { estadoNuevo = "RECHAZADA" }, Ct); Assert.Equal(HttpStatusCode.OK, rechazada.StatusCode);
        Assert.Equal(media, await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{principal.Id}/calificacion", Ct));
        var sin = (await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{vacio.Id}/calificacion", Ct))!; Assert.Null(sin.Promedio); Assert.Equal(0, sin.Cantidad);
        using var inexistente = await publico.GetAsync("/api/v1/presentadores/2147483647/calificacion", Ct); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        using var invalida = await publico.GetAsync("/api/v1/presentadores/0/calificacion", Ct); Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
    }
}
