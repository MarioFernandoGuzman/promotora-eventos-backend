using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Ticketing;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Api.Tests;

public sealed class ComprasApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CompraCongelaD15AgrupaItemsAuditaYRepiteSinDuplicar()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await LoginAsync(client, RolUsuario.CLIENTE, ct);
        string key = Guid.NewGuid().ToString();
        var body = new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1), new ItemCompra(datos.TipoId, 2) } };
        var response = await CompraAsync(client, body, key, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var compra = (await response.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        Assert.Equal("PENDIENTE", compra.Estado);
        Assert.Equal(300m, compra.Subtotal); Assert.Equal(30m, compra.ComisionPlataforma); Assert.Equal(39.60m, compra.Impuestos); Assert.Equal(369.60m, compra.Total);
        Assert.Equal(3, Assert.Single(compra.Items).Cantidad);
        Assert.Equal(TimeSpan.FromMinutes(15), compra.ExpiraEn - compra.FechaCompra);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Precio, 120m), ct);
        // Se conserva la compra aunque la solicitud, precio y estado del evento cambien.
        await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, "FINALIZADO"), ct);
        var repetida = await CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1) } }, key, ct);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        var original = (await repetida.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        Assert.Equal(compra.PublicId, original.PublicId); Assert.Equal(compra.Total, original.Total);
        Assert.Equal(100m, Assert.Single(original.Items).PrecioUnitario);
        Assert.Equal(1, await db.Compras.CountAsync(c => c.IdempotencyKey == key, ct));
        Assert.Equal(7, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
        var id = await db.Compras.Where(c => c.IdempotencyKey == key).Select(c => c.Id).SingleAsync(ct);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == id.ToString() && a.Accion == "CREATE", ct));
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == id, ct));
        Assert.False(await db.Boletos.AnyAsync(b => db.DetalleCompra.Any(d => d.Id == b.DetalleCompraId && d.CompraId == id), ct));
        var schema = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        var operation = schema.GetProperty("paths").GetProperty("/api/v1/compras").GetProperty("post");
        Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("required").GetBoolean());
        Assert.True(schema.GetProperty("components").GetProperty("schemas").GetProperty("CrearCompraRequest").TryGetProperty("example", out _));
    }

    [Fact]
    public async Task AgotadoRevierteTodasLasReservasYNoCreaCompraNiAuditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, ct);
        var sinCupo = new TipoEntradaRow { EventoId = datos.EventoId, Nombre = "Agotada", Precio = 100m, CupoTotal = 0, CupoDisponible = 0 };
        db.TiposEntrada.Add(sinCupo); await db.SaveChangesAsync(ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); await LoginAsync(client, RolUsuario.CLIENTE, ct);
        var key = Guid.NewGuid().ToString();
        int auditorias = await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras", ct);
        var response = await CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 2), new ItemCompra(sinCupo.Id, 1) } }, key, ct);
        await ProblemAsync(response, HttpStatusCode.Conflict, ct);
        Assert.False(await db.Compras.AnyAsync(c => c.IdempotencyKey == key, ct));
        Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
        Assert.Equal(auditorias, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras", ct));
    }

    [Theory]
    [InlineData("PROGRAMADO", -1, 1)]
    [InlineData("EN_VENTA", 1, 2)]
    [InlineData("EN_VENTA", -2, -1)]
    public async Task RechazaEstadoOVentanaNoVigente(string estado, int inicioDias, int finDias)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, ct);
        var now = DateTime.UtcNow;
        await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, estado)
            .SetProperty(e => e.FechaInicioVenta, now.AddDays(inicioDias)).SetProperty(e => e.FechaFinVenta, now.AddDays(finDias)), ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); await LoginAsync(client, RolUsuario.CLIENTE, ct);
        await ProblemAsync(await CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1) } }, Guid.NewGuid().ToString(), ct), HttpStatusCode.Conflict, ct);
        Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task ValidaHeaderCantidadMaximoEventoYActividadDeEntrada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 20, ct);
        var otro = await CompraFixture.CrearAsync(db, 20, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); await LoginAsync(client, RolUsuario.CLIENTE, ct);
        object Body(int tipo, int cantidad) => new { eventoId = datos.EventoId, items = new[] { new ItemCompra(tipo, cantidad) } };
        await ProblemAsync(await CompraAsync(client, Body(datos.TipoId, 1), null, ct), HttpStatusCode.BadRequest, ct);
        await ProblemAsync(await CompraAsync(client, Body(datos.TipoId, 1), new string('x', 101), ct), HttpStatusCode.BadRequest, ct);
        await ProblemAsync(await CompraAsync(client, Body(datos.TipoId, 0), Guid.NewGuid().ToString(), ct), HttpStatusCode.BadRequest, ct);
        await ProblemAsync(await CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 6), new ItemCompra(datos.TipoId, 5) } }, Guid.NewGuid().ToString(), ct), HttpStatusCode.UnprocessableEntity, ct);
        await ProblemAsync(await CompraAsync(client, Body(otro.TipoId, 1), Guid.NewGuid().ToString(), ct), HttpStatusCode.UnprocessableEntity, ct);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, false), ct);
        await ProblemAsync(await CompraAsync(client, Body(datos.TipoId, 1), Guid.NewGuid().ToString(), ct), HttpStatusCode.UnprocessableEntity, ct);
        await ProblemAsync(await CompraAsync(client, new { eventoId = int.MaxValue, items = new[] { new ItemCompra(datos.TipoId, 1) } }, Guid.NewGuid().ToString(), ct), HttpStatusCode.NotFound, ct);
        Assert.False(await db.Compras.AnyAsync(c => c.EventoId == datos.EventoId, ct));
    }

    [Fact]
    public async Task RepeticionConcurrenteDeClavePropiaCreaUnaSolaCompra()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); await LoginAsync(client, RolUsuario.CLIENTE, ct);
        string key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 2) } }, key, ct)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, await db.Compras.CountAsync(c => c.IdempotencyKey == key, ct));
        Assert.Equal(8, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task DosClientesConMismaClaveNoRevelanCompraAjenaNiDuplicanReserva()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var primero = factory.CreateClient(); using var segundo = factory.CreateClient();
        await LoginAsync(primero, RolUsuario.CLIENTE, ct); await LoginAsync(segundo, RolUsuario.CLIENTE, ct);
        string key = Guid.NewGuid().ToString();
        var body = new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 2) } };
        var responses = await Task.WhenAll(CompraAsync(primero, body, key, ct), CompraAsync(segundo, body, key, ct));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        await ProblemAsync(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, ct);
        Assert.Equal(1, await db.Compras.CountAsync(c => c.IdempotencyKey == key, ct));
        Assert.Equal(8, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Theory]
    [InlineData(RolUsuario.ADMINISTRADOR)]
    [InlineData(RolUsuario.DUENO_RECINTO)]
    public async Task ComprasSoloPermiteClienteAutenticado(RolUsuario rol)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var body = new { eventoId = 1, items = new[] { new ItemCompra(1, 1) } };
        await ProblemAsync(await CompraAsync(client, body, Guid.NewGuid().ToString(), ct), HttpStatusCode.Unauthorized, ct);
        await LoginAsync(client, rol, ct);
        await ProblemAsync(await CompraAsync(client, body, Guid.NewGuid().ToString(), ct), HttpStatusCode.Forbidden, ct);
    }

    [Fact]
    public async Task ClienteDesactivadoConJwtAnteriorNoCompraNiReutilizaClave()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); int usuarioId = await LoginAsync(client, RolUsuario.CLIENTE, ct);
        string key = Guid.NewGuid().ToString();
        var body = new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1) } };
        Assert.Equal(HttpStatusCode.Created, (await CompraAsync(client, body, key, ct)).StatusCode);
        await db.Usuarios.Where(u => u.Id == usuarioId && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), ct);
        await ProblemAsync(await CompraAsync(client, body, key, ct), HttpStatusCode.Unauthorized, ct);
        await ProblemAsync(await CompraAsync(client, body, Guid.NewGuid().ToString(), ct), HttpStatusCode.Unauthorized, ct);
    }

    [Theory]
    [InlineData("recinto", HttpStatusCode.Conflict)]
    [InlineData("categoria", HttpStatusCode.Conflict)]
    [InlineData("localidad", HttpStatusCode.UnprocessableEntity)]
    public async Task RechazaPadresInactivos(string recurso, HttpStatusCode codigo)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        int recintoId = await db.Eventos.Where(e => e.Id == datos.EventoId).Select(e => e.RecintoId).SingleAsync(ct);
        if (recurso == "recinto") await db.Recintos.Where(r => r.Id == recintoId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Activo, false), ct);
        if (recurso == "categoria")
        {
            var categoria = new CategoriaEventoRow { Nombre = Guid.NewGuid().ToString("N"), Activo = false };
            db.CategoriasEvento.Add(categoria); await db.SaveChangesAsync(ct);
            await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoriaId, categoria.Id), ct);
        }
        if (recurso == "localidad")
        {
            await db.Recintos.Where(r => r.Id == recintoId).ExecuteUpdateAsync(s => s.SetProperty(r => r.CapacidadTotal, 10), ct);
            var localidad = new LocalidadRow { RecintoId = recintoId, Nombre = "Inactiva", Capacidad = 10, Activo = false };
            db.Localidades.Add(localidad); await db.SaveChangesAsync(ct);
            await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.LocalidadId, localidad.Id), ct);
        }
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); await LoginAsync(client, RolUsuario.CLIENTE, ct);
        await ProblemAsync(await CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1) } }, Guid.NewGuid().ToString(), ct), codigo, ct);
        Assert.Equal(10, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task CompraGratuitaSePersisteSinPagosNiMovimientos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Precio, 0m), ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); await LoginAsync(client, RolUsuario.CLIENTE, ct);
        var response = await CompraAsync(client, new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 2) } }, Guid.NewGuid().ToString(), ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var compra = (await response.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        Assert.Equal(0m, compra.Total); Assert.Equal(0m, compra.ComisionPlataforma); Assert.Equal(0m, compra.Impuestos);
        int id = await db.Compras.Where(c => c.PublicId == compra.PublicId).Select(c => c.Id).SingleAsync(ct);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == id, ct));
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == id, ct));
    }

    [Fact]
    public async Task FalloDeAuditoriaRevierteCompraDetallesYCupo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); int usuario = await LoginAsync(client, RolUsuario.CLIENTE, ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<CrearCompraHandler>(scope.ServiceProvider, new Actual(usuario), new AuditFallando());
        string key = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new CrearCompraCommand(datos.EventoId, [new ItemCompra(datos.TipoId, 2)], key), ct));
        Assert.False(await db.Compras.AnyAsync(c => c.IdempotencyKey == key, ct));
        Assert.False(await db.DetalleCompra.AnyAsync(d => d.TipoEntradaId == datos.TipoId, ct));
        Assert.Equal(10, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task HandlerCompruebaRolAunqueSeInvoqueSinController()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); int usuario = await LoginAsync(client, RolUsuario.ADMINISTRADOR, ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<CrearCompraHandler>(scope.ServiceProvider, new Actual(usuario));
        var result = await handler.HandleAsync(new CrearCompraCommand(1, [new ItemCompra(1, 1)], Guid.NewGuid().ToString()), ct);
        Assert.True(result.IsFailure);
        Assert.Equal(Promotora.Domain.Shared.ErrorType.Forbidden, Assert.Single(result.Errors).Type);
    }

    private sealed record Actual(int? UsuarioId) : IUsuarioActual
    {
        public string? IpOrigen => null;
    }

    private sealed class AuditFallando : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => throw new InvalidOperationException("Fallo simulado de auditoría.");
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => throw new InvalidOperationException("Fallo simulado de auditoría.");
    }

    private async Task<int> LoginAsync(HttpClient client, RolUsuario rol, CancellationToken ct)
    {
        await using var db = fixture.CreateContext();
        var clock = new SystemClock(TimeProvider.System);
        string email = $"{Guid.NewGuid():N}@example.test", password = Guid.NewGuid().ToString("N");
        var repository = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, clock));
        var usuario = await repository.CreateAsync(new Usuario(0, email, new IdentityPasswordService().Hash(password), "Prueba", "Compra", rol, "ACTIVO", false, clock.UtcNow),
            new PerfilNuevo(Empresa: rol == RolUsuario.DUENO_RECINTO ? "Prueba" : null), null, null, false, ct);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenDto>(ct))!.AccessToken);
        return usuario.Value.Id;
    }

    private static async Task<HttpResponseMessage> CompraAsync(HttpClient client, object body, string? key, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras") { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, ct);
    }

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken ct)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(problem.TryGetProperty("publicId", out _));
    }
}
