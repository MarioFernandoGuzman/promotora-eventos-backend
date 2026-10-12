using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Reviews;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class ResenaPropiaApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task AutorEditaRetiraYReenviaSinBorrarNiMantenerPublicacionAnterior()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient(); using var ajeno = factory.CreateClient(); using var publico = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct); await soporte.LoginAsync(ajeno, RolUsuario.CLIENTE, Ct);
        var original = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct);
        var mis = (await cliente.GetFromJsonAsync<PaginaDto<ResenaDto>>("/api/v1/resenas/mis", Ct))!; Assert.Equal(original, Assert.Single(mis.Items));
        var otras = (await ajeno.GetFromJsonAsync<PaginaDto<ResenaDto>>("/api/v1/resenas/mis", Ct))!; Assert.Empty(otras.Items);
        using var noAutor = await ajeno.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", new { calificacion = 3, comentario = "Otro", estadoEsperado = "PENDIENTE" }, Ct); Assert.Equal(HttpStatusCode.NotFound, noAutor.StatusCode);
        using var retiroAjeno = await ajeno.PostAsJsonAsync($"/api/v1/resenas/{original.Id}/retirar", new { estadoEsperado = "PENDIENTE" }, Ct); Assert.Equal(HttpStatusCode.NotFound, retiroAjeno.StatusCode);
        using var rol = await admin.GetAsync("/api/v1/resenas/mis", Ct); Assert.Equal(HttpStatusCode.Forbidden, rol.StatusCode);
        using var anonima = await publico.GetAsync("/api/v1/resenas/mis", Ct); Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        await using var db = postgres.CreateContext(); var presentador = new PresentadorRow { Nombre = "Prueba edición", Tipo = "ARTISTA" };
        db.Presentadores.Add(presentador); await db.SaveChangesAsync(Ct); db.EventoPresentadores.Add(new EventoPresentadorRow { EventoId = original.EventoId, PresentadorId = presentador.Id }); await db.SaveChangesAsync(Ct);
        using var aprobada = await admin.PatchAsJsonAsync($"/api/v1/resenas/{original.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode);
        Assert.Equal(5m, (await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{presentador.Id}/calificacion", Ct))!.Promedio);
        var cuerpo = new { calificacion = 3, comentario = new string('x', 510), estadoEsperado = "APROBADA" };
        using var editada = await cliente.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", cuerpo, Ct); Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        var pendiente = (await editada.Content.ReadFromJsonAsync<ResenaDto>(Ct))!; Assert.Equal("PENDIENTE", pendiente.Estado); Assert.Equal(original.FechaCreacion, pendiente.FechaCreacion);
        using var repetida = await cliente.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", cuerpo, Ct); Assert.Equal(HttpStatusCode.OK, repetida.StatusCode); Assert.Equal(pendiente, await repetida.Content.ReadFromJsonAsync<ResenaDto>(Ct));
        Assert.Empty((await publico.GetFromJsonAsync<PaginaDto<ResenaDto>>($"/api/v1/eventos/{original.EventoId}/resenas", Ct))!.Items);
        Assert.Null((await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{presentador.Id}/calificacion", Ct))!.Promedio);
        using var revision = await admin.PatchAsJsonAsync($"/api/v1/resenas/{original.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        Assert.Equal(3m, (await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{presentador.Id}/calificacion", Ct))!.Promedio);
        for (int i = 0; i < 2; i++)
        { using var retirada = await cliente.PostAsJsonAsync($"/api/v1/resenas/{original.Id}/retirar", new { estadoEsperado = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.OK, retirada.StatusCode); }
        Assert.Null((await publico.GetFromJsonAsync<CalificacionPresentadorDto>($"/api/v1/presentadores/{presentador.Id}/calificacion", Ct))!.Promedio);
        Assert.Equal(1, await db.Resenas.CountAsync(r => r.Id == original.Id, Ct));
        var auditorias = await db.LogsAuditoria.AsNoTracking().Where(l => l.EntidadAfectada == "resenas" && l.EntidadId == original.Id.ToString() && l.Accion == "UPDATE").ToArrayAsync(Ct);
        Assert.Single(auditorias, l => l.Detalle?.RootElement.GetProperty("motivo").GetString() == "ResenaRetirada");
        using var nueva = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = original.EventoId, calificacion = 4 }, Ct); Assert.Equal(HttpStatusCode.Conflict, nueva.StatusCode);
        using var reenvio = await cliente.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", new { calificacion = 4, comentario = "Reenviada", estadoEsperado = "RECHAZADA" }, Ct); Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);
        using var largo = await cliente.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", new { calificacion = 4, comentario = new string('x', 511), estadoEsperado = "PENDIENTE" }, Ct); Assert.Equal(HttpStatusCode.BadRequest, largo.StatusCode);
    }
    [Fact]
    public async Task EditarYRetirarCompitenContraElMismoEstadoEsperado()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        var original = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct);
        using var aprobada = await admin.PatchAsJsonAsync($"/api/v1/resenas/{original.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode);
        var editar = cliente.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", new { calificacion = 4, comentario = "Cambiada", estadoEsperado = "APROBADA" }, Ct);
        var retirar = cliente.PostAsJsonAsync($"/api/v1/resenas/{original.Id}/retirar", new { estadoEsperado = "APROBADA" }, Ct);
        using var a = await editar; using var b = await retirar;
        Assert.Equal(1, new[] { a, b }.Count(r => r.StatusCode == HttpStatusCode.OK)); Assert.Equal(1, new[] { a, b }.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = postgres.CreateContext(); Assert.Equal(1, await db.LogsAuditoria.CountAsync(l => l.EntidadAfectada == "resenas" && l.EntidadId == original.Id.ToString() && l.Accion == "UPDATE", Ct));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditoriaFallidaRevierteEdicionORetiro(bool retirar)
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct); var original = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct);
        await using var fallando = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddScoped<IAuditLogger>(sp => new AuditFallando(new EfAuditLogger(sp.GetRequiredService<PromotoraDbContext>(), sp.GetRequiredService<IClock>())))));
        using var prueba = fallando.CreateClient(); prueba.DefaultRequestHeaders.Authorization = cliente.DefaultRequestHeaders.Authorization;
        using var respuesta = retirar ? await prueba.PostAsJsonAsync($"/api/v1/resenas/{original.Id}/retirar", new { estadoEsperado = "PENDIENTE" }, Ct)
            : await prueba.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", new { calificacion = 4, comentario = "Nueva", estadoEsperado = "PENDIENTE" }, Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        var mis = (await cliente.GetFromJsonAsync<PaginaDto<ResenaDto>>("/api/v1/resenas/mis", Ct))!; Assert.Equal(original, Assert.Single(mis.Items));
        await using var db = postgres.CreateContext(); Assert.False(await db.LogsAuditoria.AnyAsync(l => l.EntidadAfectada == "resenas" && l.EntidadId == original.Id.ToString() && l.Accion != "CREATE", Ct));
    }
    [Theory]
    [InlineData("INACTIVO")]
    [InlineData("PASSWORD")]
    public async Task ClienteInhabilitadoNoConsultaNiModificaConTokenAnterior(string cambio)
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct); var original = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct);
        await using var db = postgres.CreateContext(); int perfil = await db.Resenas.Where(r => r.Id == original.Id).Select(r => r.ClienteId).SingleAsync(Ct);
        int usuario = await db.Clientes.Where(c => c.Id == perfil).Select(c => c.UsuarioId).SingleAsync(Ct);
        if (cambio == "INACTIVO") await db.Usuarios.Where(u => u.Id == usuario && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        else await db.Usuarios.Where(u => u.Id == usuario).ExecuteUpdateAsync(s => s.SetProperty(u => u.DebeCambiarPassword, true), Ct);
        using var lectura = await cliente.GetAsync("/api/v1/resenas/mis", Ct); Assert.Contains(lectura.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        using var escritura = await cliente.PutAsJsonAsync($"/api/v1/resenas/{original.Id}", new { calificacion = 4, estadoEsperado = "PENDIENTE" }, Ct); Assert.Contains(escritura.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Equal("PENDIENTE", await db.Resenas.Where(r => r.Id == original.Id).Select(r => r.Estado).SingleAsync(Ct));
    }
    private sealed class AuditFallando(IAuditLogger inner) : IAuditLogger
    {
        public Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, CancellationToken ct) => WriteAsync(usuario, accion, entidad, id, ip, null, ct);
        public async Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            await inner.WriteAsync(usuario, accion, entidad, id, ip, detalle, ct);
            if (entidad == "resenas" && accion == "UPDATE") throw new InvalidOperationException("Fallo de auditoría en prueba.");
        }
    }
}
