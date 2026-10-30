using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing.Numeracion;
using Promotora.Application.Venues;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Catalog.Persistence;

namespace Promotora.Api.Tests;

public sealed partial class VenuesApiTests
{
    [Fact]
    public async Task DistribucionNumeradaValidaPropiedadPublicacionYBloqueoDeMesa()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var other = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var cliente = await CreateUserAsync(RolUsuario.CLIENTE, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); using var publico = factory.CreateClient();
        await LoginAsync(client, owner, ct);
        var recinto = (await (await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Numerado", capacidadTotal = 10 }, ct)).Content.ReadFromJsonAsync<RecintoDto>(ct))!;
        var localidad = (await (await client.PostAsJsonAsync($"/api/v1/recintos/{recinto.Id}/localidades", new DatosLocalidadDto("Mesas", 2, null, null), ct)).Content.ReadFromJsonAsync<LocalidadDto>(ct))!;
        await using var db = fixture.CreateContext();
        var evento = new EventoRow
        {
            RecintoId = recinto.Id,
            CategoriaId = await db.CategoriasEvento.Select(c => c.Id).FirstAsync(ct),
            CreadoPor = owner.Id,
            Nombre = "Concierto",
            Estado = "EN_VENTA",
            FechaInicio = DateTime.UtcNow.AddDays(2),
            FechaInicioVenta = DateTime.UtcNow.AddDays(-1),
            FechaFinVenta = DateTime.UtcNow.AddDays(1)
        };
        db.Eventos.Add(evento); await db.SaveChangesAsync(ct);
        var tipo = new TipoEntradaRow { EventoId = evento.Id, LocalidadId = localidad.Id, Nombre = "Mesa", Precio = 100, CupoTotal = 2, CupoDisponible = 2 };
        db.TiposEntrada.Add(tipo); await db.SaveChangesAsync(ct);
        string root = $"/api/v1/eventos/{evento.Id}/localidades/{localidad.Id}";
        var datos = new DatosDistribucion(null, [new(null, "m1", "Mesa 1", "COMPLETA", tipo.Id, .5m, .5m)],
            [new(null, "m1", "M1-S1", null, tipo.Id, .4m, .5m), new(null, "m1", "M1-S2", null, tipo.Id, .6m, .5m)]);
        var response = await client.PutAsJsonAsync(root + "/distribucion", datos, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plano = (await response.Content.ReadFromJsonAsync<DistribucionDto>(ct))!;
        Assert.Equal(2, plano.TotalAsientos);
        await ProblemAsync(await publico.GetAsync(root + "/asientos", ct), 404, ct);
        await LoginAsync(client, other, ct);
        await ProblemAsync(await client.GetAsync(root + "/distribucion", ct), 404, ct);
        await LoginAsync(client, cliente, ct);
        await ProblemAsync(await client.PostAsync(root + "/distribucion/publicar", null, ct), 403, ct);
        await LoginAsync(client, owner, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(root + "/distribucion/publicar", null, ct)).StatusCode);
        var pagina = (await publico.GetFromJsonAsync<DistribucionDto>(root + "/asientos?pageSize=1", ct))!;
        Assert.Single(pagina.Asientos); Assert.Equal(2, pagina.TotalAsientos);
        await ProblemAsync(await client.PutAsJsonAsync(root + "/distribucion", datos, ct), 409, ct);
        await ProblemAsync(await client.PostAsJsonAsync(root + "/asientos/bloquear", new[] { plano.Asientos[0].Id }, ct), 409, ct);
        var ids = plano.Asientos.Select(a => a.Id).ToArray();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/asientos/bloquear", ids, ct)).StatusCode);
        Assert.Equal(0, await db.TiposEntrada.Where(t => t.Id == tipo.Id).Select(t => t.CupoDisponible).SingleAsync(ct));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/asientos/desbloquear", ids, ct)).StatusCode);
        Assert.Equal(2, await db.TiposEntrada.Where(t => t.Id == tipo.Id).Select(t => t.CupoDisponible).SingleAsync(ct));
        Assert.True(await db.LogsAuditoria.AnyAsync(l => l.EntidadAfectada == "distribuciones_evento", ct));
    }
}
