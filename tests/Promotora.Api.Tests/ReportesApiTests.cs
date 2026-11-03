using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Api.Tests;

public sealed class ReportesApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task VentasOcupacionPermisosYFiltrosCoincidenConCompraConocida()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var publico = factory.CreateClient(); using var cliente = factory.CreateClient();
        using var dueno = factory.CreateClient(); using var admin = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres);
        await soporte.LoginAsync(cliente, RolUsuario.CLIENTE, Ct);
        await soporte.LoginAsync(dueno, RolUsuario.DUENO_RECINTO, Ct);
        await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        using var anonima = await publico.GetAsync("/api/v1/reportes/R-01", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        foreach (var actor in new[] { cliente, dueno })
        {
            using var prohibida = await actor.GetAsync("/api/v1/reportes/R-01", Ct);
            Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
        }
        await using var db = postgres.CreateContext();
        var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        int evento = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.EventoId).SingleAsync(Ct);
        int recinto = await db.Eventos.Where(e => e.Id == evento).Select(e => e.RecintoId).SingleAsync(Ct);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            await db.Recintos.Where(r => r.Id == recinto).ExecuteUpdateAsync(s => s.SetProperty(r => r.CapacidadTotal, 5), Ct);
            var localidad = new LocalidadRow { RecintoId = recinto, Nombre = "General", Capacidad = 5 };
            db.Localidades.Add(localidad); await db.SaveChangesAsync(Ct);
            await db.TiposEntrada.Where(t => t.Id == venta.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.LocalidadId, localidad.Id), Ct);
            await tx.CommitAsync(Ct);
        }
        foreach (string codigo in new[] { "R-01", "R-02", "R-03", "R-04" })
        {
            var reporte = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/{codigo}?eventoId={evento}&moneda=GTQ&pageSize=1", Ct);
            var seccion = reporte.GetProperty("secciones")[0];
            Assert.Equal(369.60m, seccion.GetProperty("totales")[0].GetProperty("total").GetDecimal());
            Assert.Equal(1, seccion.GetProperty("totalFilas").GetInt64());
        }
        var ocupacion = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-05?eventoId={evento}", Ct);
        Assert.Equal(3, ocupacion.GetProperty("secciones")[0].GetProperty("totales")[0].GetProperty("cupoOcupado").GetDecimal());
        using var incompatible = await admin.GetAsync("/api/v1/reportes/R-05?moneda=GTQ", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, incompatible.StatusCode);
        using var paginaInvalida = await admin.GetAsync("/api/v1/reportes/R-01?pageSize=101", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, paginaInvalida.StatusCode);
        var segunda = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-01?eventoId={evento}&page=2&pageSize=1", Ct);
        var pagina = segunda.GetProperty("secciones")[0];
        Assert.Equal(0, pagina.GetProperty("filas").GetArrayLength());
        Assert.Equal(369.60m, pagina.GetProperty("totales")[0].GetProperty("total").GetDecimal());
    }
}
