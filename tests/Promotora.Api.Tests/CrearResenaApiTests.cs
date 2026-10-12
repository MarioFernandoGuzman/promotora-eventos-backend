using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Reviews;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CrearResenaApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task SoloUsadoYDiezCreacionesConcurrentesProducenUnaResenaAuditada()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        var (venta, _) = await ResenasTestSupport.VentaAsync(postgres, factory, cliente, Ct);
        await using var db = postgres.CreateContext(); int evento = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.EventoId).SingleAsync(Ct);
        using var sinUsar = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = evento, calificacion = 5 }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sinUsar.StatusCode);
        await ResenasTestSupport.UsarAsync(postgres, admin, venta, Ct);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = evento, calificacion = 5, comentario = new string('x', 510) }, Ct)));
        try
        {
            Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK)); Assert.Equal(9, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            var creada = (await respuestas.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<ResenaDto>(Ct))!;
            Assert.Equal("PENDIENTE", creada.Estado); Assert.Equal(510, creada.Comentario!.Length); Assert.Equal(DateTimeKind.Utc, creada.FechaCreacion.Kind);
            Assert.Equal(1, await db.Resenas.CountAsync(r => r.EventoId == evento, Ct));
            Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "resenas" && a.EntidadId == creada.Id.ToString() && a.Accion == "CREATE", Ct));
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }
    [Fact]
    public async Task ContratoDePermisosValidacionYEventoAjeno()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var cliente = factory.CreateClient(); using var admin = factory.CreateClient(); using var dueno = factory.CreateClient(); using var publico = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct); await soporte.LoginAsync(dueno, RolUsuario.DUENO_RECINTO, Ct);
        foreach (var (actor, esperado) in new[] { (publico, HttpStatusCode.Unauthorized), (admin, HttpStatusCode.Forbidden), (dueno, HttpStatusCode.Forbidden) })
        { using var response = await actor.PostAsJsonAsync("/api/v1/resenas", new { eventoId = 1, calificacion = 5 }, Ct); Assert.Equal(esperado, response.StatusCode); }
        var (venta, _) = await ResenasTestSupport.VentaAsync(postgres, factory, cliente, Ct); await using var db = postgres.CreateContext();
        int evento = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.EventoId).SingleAsync(Ct);
        using var largo = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = evento, calificacion = 5, comentario = new string('x', 511) }, Ct); Assert.Equal(HttpStatusCode.BadRequest, largo.StatusCode);
        using var nota = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = evento, calificacion = 6 }, Ct); Assert.Equal(HttpStatusCode.BadRequest, nota.StatusCode);
        using var inexistente = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = int.MaxValue, calificacion = 5 }, Ct); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        await ResenasTestSupport.UsarAsync(postgres, admin, venta, Ct); var otro = await CompraFixture.CrearAsync(db, 5, Ct);
        using var ajeno = await cliente.PostAsJsonAsync("/api/v1/resenas", new { eventoId = otro.EventoId, calificacion = 5 }, Ct); Assert.Equal(HttpStatusCode.UnprocessableEntity, ajeno.StatusCode);
    }
}
