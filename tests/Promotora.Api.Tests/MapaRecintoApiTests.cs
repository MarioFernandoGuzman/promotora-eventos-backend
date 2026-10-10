using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Venues;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Catalog.Persistence;

namespace Promotora.Api.Tests;

public sealed partial class VenuesApiTests
{
    [Fact]
    public async Task MapaPublicoFiltraInactivosBorradoresYEventosAjenosSinExponerIdentidadNiModificarCupo()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await LoginAsync(client, owner, ct);
        var recinto = (await (await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Mapa", capacidadTotal = 100, imagenPlanoUrl = "/planos/prueba.png" }, ct)).Content.ReadFromJsonAsync<RecintoDto>(ct))!;
        var otro = (await (await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Otro mapa", capacidadTotal = 100 }, ct)).Content.ReadFromJsonAsync<RecintoDto>(ct))!;
        var localidad = (await (await client.PostAsJsonAsync($"/api/v1/recintos/{recinto.Id}/localidades", new DatosLocalidadDto("Palco", 60,
            new PoligonoDto("polygon", [new[] { 10d, 20d }, new[] { 80d, 20d }, new[] { 80d, 60d }]), "#3366CC"), ct)).Content.ReadFromJsonAsync<LocalidadDto>(ct))!;
        var inactiva = (await (await client.PostAsJsonAsync($"/api/v1/recintos/{recinto.Id}/localidades", new { nombre = "Oculta", capacidad = 40 }, ct)).Content.ReadFromJsonAsync<LocalidadDto>(ct))!;
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/recintos/{recinto.Id}/localidades/{inactiva.Id}", ct)).StatusCode);
        await using var db = fixture.CreateContext();
        int category = await db.CategoriasEvento.Select(x => x.Id).FirstAsync(ct);
        var evento = new EventoRow { RecintoId = recinto.Id, CategoriaId = category, CreadoPor = owner.Id, Nombre = "Público", Estado = "EN_VENTA", FechaInicio = DateTime.UtcNow.AddDays(1), FechaCreacion = DateTime.UtcNow };
        var borrador = new EventoRow { RecintoId = recinto.Id, CategoriaId = category, CreadoPor = owner.Id, Nombre = "Privado", Estado = "BORRADOR", FechaInicio = DateTime.UtcNow.AddDays(1), FechaCreacion = DateTime.UtcNow };
        db.Eventos.AddRange(evento, borrador);
        await db.SaveChangesAsync(ct);
        db.TiposEntrada.AddRange(
            new TipoEntradaRow { EventoId = evento.Id, LocalidadId = localidad.Id, Nombre = "Palco", Precio = 150.50m, CupoTotal = 40, CupoDisponible = 12 },
            new TipoEntradaRow { EventoId = evento.Id, LocalidadId = localidad.Id, Nombre = "Inactivo", Precio = 100, CupoTotal = 20, CupoDisponible = 20, Activo = false },
            new TipoEntradaRow { EventoId = evento.Id, LocalidadId = inactiva.Id, Nombre = "Zona oculta", Precio = 100, CupoTotal = 30, CupoDisponible = 30 },
            new TipoEntradaRow { EventoId = evento.Id, Nombre = "General", Precio = 90, CupoTotal = 10, CupoDisponible = 7 });
        await db.SaveChangesAsync(ct);
        client.DefaultRequestHeaders.Authorization = null;
        string url = $"/api/v1/recintos/{recinto.Id}/mapa";
        var sinEvento = (await client.GetFromJsonAsync<MapaRecintoDto>(url, ct))!;
        Assert.Null(sinEvento.EventoId);
        Assert.Null(Assert.Single(sinEvento.Localidades).CupoDisponible);
        var mapa = (await client.GetFromJsonAsync<MapaRecintoDto>($"{url}?eventoId={evento.Id}", ct))!;
        Assert.Equal("/planos/prueba.png", mapa.ImagenPlanoUrl);
        Assert.Equal("EN_VENTA", mapa.EstadoEvento);
        var zona = Assert.Single(mapa.Localidades);
        Assert.Equal(localidad.Id, zona.Id);
        Assert.Equal(3, zona.Geometria!.Points.Count);
        Assert.Equal(12, zona.CupoDisponible);
        Assert.Equal(150.50m, Assert.Single(zona.TiposEntrada).Precio);
        Assert.Equal(7, Assert.Single(mapa.EntradasGenerales).CupoDisponible);
        var json = await client.GetFromJsonAsync<JsonElement>($"{url}?eventoId={evento.Id}", ct);
        Assert.False(json.TryGetProperty("duenoId", out _));
        Assert.False(json.TryGetProperty("usuarioId", out _));
        Assert.False(json.TryGetProperty("creadoPor", out _));
        Assert.Equal(12, await db.TiposEntrada.Where(x => x.EventoId == evento.Id && x.Nombre == "Palco").Select(x => x.CupoDisponible).SingleAsync(ct));
        await ProblemAsync(await client.GetAsync($"{url}?eventoId={borrador.Id}", ct), 404, ct);
        await ProblemAsync(await client.GetAsync($"{url}?eventoId=0", ct), 400, ct);
        await ProblemAsync(await client.GetAsync("/api/v1/recintos/2147483647/mapa", ct), 404, ct);
        await ProblemAsync(await client.GetAsync($"/api/v1/recintos/{otro.Id}/mapa?eventoId={evento.Id}", ct), 404, ct);
        await db.Eventos.Where(x => x.Id == evento.Id && x.Estado == "EN_VENTA").ExecuteUpdateAsync(x => x.SetProperty(y => y.Estado, "CANCELADO"), ct);
        await ProblemAsync(await client.GetAsync($"{url}?eventoId={evento.Id}", ct), 404, ct);
        await LoginAsync(client, owner, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/recintos/{recinto.Id}", ct)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        await ProblemAsync(await client.GetAsync(url, ct), 404, ct);
        var doc = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        Assert.False(doc.GetProperty("paths").GetProperty("/api/v1/recintos/{id}/mapa").GetProperty("get").TryGetProperty("security", out _));
    }
}
