using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Venues;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;
using Promotora.WebApi.Catalog;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    [Fact]
    public async Task DuenoCompletaCatalogoYMapaPublicosConAuditoriaSinCambiosDelPropietarioAjeno()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient(); using var other = factory.CreateClient(); using var publico = factory.CreateClient();
        var user = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var ajeno = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        await LoginAsync(owner, user, ct); await LoginAsync(other, ajeno, ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        var datosLocalidad = new DatosLocalidadDto("VIP", 60,
            new PoligonoDto("polygon", [new[] { 0d, 0d }, new[] { 2d, 0d }, new[] { 2d, 3d }, new[] { 0d, 3d }]), "#3366CC");
        var localidad = await PostAsync<LocalidadDto>(owner, $"/api/v1/recintos/{recinto}/localidades", datosLocalidad, ct);
        var datosEvento = DatosEvento(recinto) with { Nombre = Guid.NewGuid().ToString("N") };
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", datosEvento, ct);
        var tipo = await PostAsync<TipoEntradaDto>(owner, $"/api/v1/eventos/{evento.Id}/tipos-entrada", new CrearTipoEntradaRequest("VIP", 50, 40, localidad.Id), ct);
        var plano = await ImagenAsync(owner, $"/api/v1/recintos/{recinto}/imagen-plano", ct);
        var foto = await ImagenAsync(owner, $"/api/v1/eventos/{evento.Id}/imagen", ct);
        await ProblemAsync(await other.PutAsJsonAsync($"/api/v1/recintos/{recinto}", new { nombre = "Ajeno", capacidadTotal = 100 }, ct), 403, ct);
        await ProblemAsync(await other.PutAsJsonAsync($"/api/v1/recintos/{recinto}/localidades/{localidad.Id}", datosLocalidad with { Nombre = "Ajena" }, ct), 403, ct);
        await ProblemAsync(await other.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}", datosEvento with { Nombre = "Ajeno" }, ct), 403, ct);
        await ProblemAsync(await other.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/tipos-entrada/{tipo.Id}", new EditarTipoEntradaRequest("Ajena", 1, 1), ct), 403, ct);
        foreach (var cambio in new[] { new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), new EstadoEventoRequest("PROGRAMADO", "EN_VENTA") })
            Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", cambio, ct)).StatusCode);

        var lista = (await publico.GetFromJsonAsync<PaginaDto<EventoResumenPublicoDto>>($"/api/v1/catalogo/eventos?texto={datosEvento.Nombre}&page=1&pageSize=1", ct))!;
        Assert.Equal(evento.Id, Assert.Single(lista.Items).Id);
        var detalle = (await publico.GetFromJsonAsync<EventoPublicoDto>($"/api/v1/catalogo/eventos/{evento.Id}", ct))!;
        Assert.Equal("EN_VENTA", detalle.Estado); Assert.Equal(datosEvento.Nombre, detalle.Nombre);
        Assert.Equal(foto.Url, detalle.ImagenUrl); Assert.Equal(plano.Url, detalle.Recinto.ImagenPlanoUrl);
        Assert.Equal(tipo.Id, Assert.Single(detalle.TiposEntrada).Id);
        var mapa = (await publico.GetFromJsonAsync<MapaRecintoDto>($"/api/v1/recintos/{recinto}/mapa?eventoId={evento.Id}", ct))!;
        Assert.Equal(plano.Url, mapa.ImagenPlanoUrl);
        var zona = Assert.Single(mapa.Localidades);
        Assert.Equal(localidad.Id, zona.Id); Assert.Equal(datosLocalidad.Geometria!.Points.Count, zona.Geometria!.Points.Count);
        Assert.Equal(40L, zona.CupoDisponible); Assert.Equal(50m, Assert.Single(zona.TiposEntrada).Precio);
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync(plano.Url, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync(foto.Url, ct)).StatusCode);
        await using var db = fixture.CreateContext();
        foreach (var (entidad, id, acciones) in new[] {
            ("recintos", recinto, new[] { "CREATE", "UPDATE" }), ("localidades", localidad.Id, new[] { "CREATE" }),
            ("eventos", evento.Id, new[] { "CREATE", "UPDATE", "UPDATE", "UPDATE" }), ("tipos_entrada", tipo.Id, new[] { "CREATE" }) })
        {
            var logs = await db.LogsAuditoria.AsNoTracking().Where(x => x.EntidadAfectada == entidad && x.EntidadId == id.ToString()).ToListAsync(ct);
            Assert.Equal(acciones.Order(), logs.Select(x => x.Accion).Order());
            Assert.All(logs, log => { Assert.Equal(user.Id, log.UsuarioId); Assert.Equal(DateTimeKind.Utc, log.FechaHora.Kind); });
        }
        Assert.Equal(40, (await db.TiposEntrada.AsNoTracking().SingleAsync(x => x.Id == tipo.Id, ct)).CupoDisponible);
        var swagger = await publico.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        Assert.Equal(54, swagger.GetProperty("paths").EnumerateObject().Sum(path => path.Value.EnumerateObject().Count(op => op.Name is "get" or "post" or "put" or "delete" or "patch")));
    }

    [Fact]
    public async Task FalloDeAuditoriaRevierteUrlYConservaLaImagenPrevia()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var original = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = original.CreateClient();
        var user = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        await LoginAsync(owner, user, ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        var previa = await ImagenAsync(owner, $"/api/v1/recintos/{recinto}/imagen-plano", ct);
        await using var failure = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IAuditLogger>(sp => new FailingUpdateAuditLogger(new EfAuditLogger(sp.GetRequiredService<PromotoraDbContext>(), sp.GetRequiredService<IClock>())))));
        using var client = failure.CreateClient();
        await LoginAsync(client, user, ct);
        await ProblemAsync(await UploadAsync(client, $"/api/v1/recintos/{recinto}/imagen-plano", ImagenFixture.Imagen(), ct), 500, ct);
        Assert.Equal(Path.GetFileName(previa.Url), Path.GetFileName(Assert.Single(Directory.GetFiles(original.ImagesRoot))));
        await using var db = fixture.CreateContext();
        Assert.Equal(previa.Url, (await db.Recintos.AsNoTracking().SingleAsync(x => x.Id == recinto, ct)).ImagenPlanoUrl);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "recintos" && x.EntidadId == recinto.ToString() && x.Accion == "UPDATE", ct));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(previa.Url, ct)).StatusCode);
    }

    private sealed class FailingUpdateAuditLogger(IAuditLogger inner) : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct)
            => WriteAsync(usuarioId, accion, entidad, entidadId, ip, null, ct);
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            if (accion == "UPDATE") throw new InvalidOperationException("Fallo simulado de auditoría.");
            return inner.WriteAsync(usuarioId, accion, entidad, entidadId, ip, detalle, ct);
        }
    }
}
