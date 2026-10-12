using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Reviews;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class ModeracionResenaApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task DosAdministradoresCompitenUnaDecisionYRepetirNoAuditaOtraVez()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var a = factory.CreateClient(); using var b = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres); await soporte.LoginAsync(a, RolUsuario.ADMINISTRADOR, Ct); await soporte.LoginAsync(b, RolUsuario.ADMINISTRADOR, Ct);
        var creada = await CrearAsync(postgres, factory, cliente, a, Ct);
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> ModerarAsync(HttpClient actor, string estado)
        {
            await salida.Task.WaitAsync(Ct); return await actor.PatchAsJsonAsync($"/api/v1/resenas/{creada.Id}/moderar", new { estadoNuevo = estado }, Ct);
        }
        var aprobacion = ModerarAsync(a, "APROBADA"); var rechazo = ModerarAsync(b, "RECHAZADA"); salida.SetResult();
        using var uno = await aprobacion; using var dos = await rechazo;
        Assert.Equal(1, new[] { uno, dos }.Count(r => r.StatusCode == HttpStatusCode.OK)); Assert.Equal(1, new[] { uno, dos }.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var ganadora = (await (uno.StatusCode == HttpStatusCode.OK ? uno : dos).Content.ReadFromJsonAsync<ResenaDto>(Ct))!;
        using var repetida = await a.PatchAsJsonAsync($"/api/v1/resenas/{creada.Id}/moderar", new { estadoNuevo = ganadora.Estado }, Ct);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode); Assert.Equal(ganadora, await repetida.Content.ReadFromJsonAsync<ResenaDto>(Ct));
        await using var db = postgres.CreateContext(); Assert.Equal(1, await db.LogsAuditoria.CountAsync(l => l.EntidadAfectada == "resenas" && l.EntidadId == creada.Id.ToString() && l.Accion == "MODERATE", Ct));
        var pendientes = (await a.GetFromJsonAsync<PaginaDto<ResenaDto>>($"/api/v1/resenas?eventoId={creada.EventoId}", Ct))!; Assert.Empty(pendientes.Items);
        var finales = (await a.GetFromJsonAsync<PaginaDto<ResenaDto>>($"/api/v1/resenas?estado={ganadora.Estado}&eventoId={creada.EventoId}&pageSize=1", Ct))!; Assert.Equal(ganadora, Assert.Single(finales.Items));
    }
    [Fact]
    public async Task ModeracionYColaExigenAdminYValidanContrato()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient(); using var dueno = factory.CreateClient(); using var publico = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct); await soporte.LoginAsync(dueno, RolUsuario.DUENO_RECINTO, Ct);
        var creada = await CrearAsync(postgres, factory, cliente, admin, Ct);
        foreach (var (actor, estado) in new[] { (publico, HttpStatusCode.Unauthorized), (cliente, HttpStatusCode.Forbidden), (dueno, HttpStatusCode.Forbidden) })
        {
            using var lectura = await actor.GetAsync("/api/v1/resenas", Ct); Assert.Equal(estado, lectura.StatusCode);
            using var escritura = await actor.PatchAsJsonAsync($"/api/v1/resenas/{creada.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(estado, escritura.StatusCode);
        }
        using var invalida = await admin.PatchAsJsonAsync($"/api/v1/resenas/{creada.Id}/moderar", new { estadoNuevo = "PENDIENTE" }, Ct); Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        using var inexistente = await admin.PatchAsJsonAsync("/api/v1/resenas/2147483647/moderar", new { estadoNuevo = "APROBADA" }, Ct); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        foreach (string filtro in new[] { "estado=OTRO", "page=0", "pageSize=101", "eventoId=0" })
        { using var respuesta = await admin.GetAsync("/api/v1/resenas?" + filtro, Ct); Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode); }
        using var rechazada = await admin.PatchAsJsonAsync($"/api/v1/resenas/{creada.Id}/moderar", new { estadoNuevo = "RECHAZADA" }, Ct); Assert.Equal(HttpStatusCode.OK, rechazada.StatusCode);
    }
    internal static async Task<ResenaDto> CrearAsync(PostgresFixture postgres, IdentityApiFactory factory, HttpClient cliente, HttpClient admin, CancellationToken ct, short calificacion = 5)
    {
        var (venta, _) = await ResenasTestSupport.VentaAsync(postgres, factory, cliente, ct); await ResenasTestSupport.UsarAsync(postgres, admin, venta, ct);
        await using var db = postgres.CreateContext(); int evento = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.EventoId).SingleAsync(ct);
        using var response = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = evento, calificacion, comentario = "Buen evento" }, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ResenaDto>(ct))!;
    }
}
