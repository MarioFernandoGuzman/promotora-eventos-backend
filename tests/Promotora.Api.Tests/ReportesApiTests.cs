using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.AI.Persistence;
using Promotora.Infrastructure.Audit.Persistence;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Tests;
using Promotora.Infrastructure.Venues.Persistence;
using UglyToad.PdfPig;

namespace Promotora.Api.Tests;

public sealed class ReportesApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompraCanceladaAportaUnIntentoSinConvertirseEnIngreso()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateClient(); using var cliente = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres);
        await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await soporte.LoginAsync(cliente, RolUsuario.CLIENTE, Ct);
        await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        var compra = await TicketingApiTestSupport.ComprarAsync(cliente, datos.EventoId, datos.TipoId, 2, Ct);
        using var cancelar = await cliente.PostAsync($"/api/v1/compras/{compra.PublicId}/cancelar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelar.StatusCode);
        var reporte = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-10?eventoId={datos.EventoId}&estado=CANCELADA", Ct);
        var total = reporte.GetProperty("secciones")[0].GetProperty("totales")[0];
        Assert.Equal(1, total.GetProperty("compras").GetInt64());
        Assert.Equal(246.40m, total.GetProperty("totalIntentado").GetDecimal());
        Assert.Equal(0, total.GetProperty("intentosPago").GetDecimal());
        var ventas = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-01?eventoId={datos.EventoId}", Ct);
        Assert.Equal(0, ventas.GetProperty("secciones")[0].GetProperty("totalFilas").GetInt64());
    }

    [Fact]
    public async Task ExportacionesSonArchivosLegiblesYQuedanAuditadas()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateClient();
        int actor = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext();
        var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        int evento = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.EventoId).SingleAsync(Ct);
        await db.Eventos.Where(e => e.Id == evento).ExecuteUpdateAsync(s => s.SetProperty(e => e.Nombre, "=SUM(1,1)"), Ct);
        using (var xlsx = await admin.GetAsync($"/api/v1/reportes/R-01?eventoId={evento}&page=2&pageSize=1&formato=xlsx", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
            using var stream = await xlsx.Content.ReadAsStreamAsync(Ct);
            using var workbook = new XLWorkbook(stream);
            Assert.Equal(369.60m, workbook.Worksheet("ventas").Cell(2, 9).GetValue<decimal>());
            Assert.False(workbook.Worksheet("ventas").Cell(2, 2).HasFormula);
            Assert.Equal("=SUM(1,1)", workbook.Worksheet("ventas").Cell(2, 2).GetString());
        }
        using (var pdf = await admin.GetAsync($"/api/v1/reportes/R-01?eventoId={evento}&formato=pdf", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
            using var document = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync(Ct));
            Assert.Contains("369.60", string.Join(" ", document.GetPages().Select(p => p.Text)));
        }
        Assert.Equal(2, await db.LogsAuditoria.CountAsync(l => l.UsuarioId == actor && l.Accion == "EXPORT" && l.EntidadAfectada == "reporte", Ct));
        using var formato = await admin.GetAsync("/api/v1/reportes/R-01?formato=csv", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, formato.StatusCode);
        var fechaLimite = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.LogsAuditoria.AddRange(Enumerable.Range(0, 501).Select(i => new LogAuditoriaRow
        { UsuarioId = actor, EntidadAfectada = $"limite_{i}", Accion = "CREATE", FechaHora = fechaLimite }));
        await db.SaveChangesAsync(Ct);
        using var exceso = await admin.GetAsync("/api/v1/reportes/R-15?desde=2002-01-01&hasta=2002-01-02&formato=pdf", Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exceso.StatusCode);
        Assert.Equal("application/problem+json", exceso.Content.Headers.ContentType!.MediaType);
        Assert.Equal(2, await db.LogsAuditoria.CountAsync(l => l.UsuarioId == actor && l.Accion == "EXPORT", Ct));
    }

    [Fact]
    public async Task OperacionCalificacionesYTiemposNoInventanTotales()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateClient(); using var cliente = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        var resena = await ModeracionResenaApiTests.CrearAsync(postgres, factory, cliente, admin, Ct, 5);
        using var moderacion = await admin.PatchAsJsonAsync($"/api/v1/resenas/{resena.Id}/moderar", new { estadoNuevo = "APROBADA" }, Ct);
        Assert.Equal(HttpStatusCode.OK, moderacion.StatusCode);
        await using var db = postgres.CreateContext();
        var presentador = new PresentadorRow { Nombre = "Reporte", Tipo = "ARTISTA" };
        db.Presentadores.Add(presentador); await db.SaveChangesAsync(Ct);
        db.EventoPresentadores.Add(new EventoPresentadorRow { EventoId = resena.EventoId, PresentadorId = presentador.Id });
        var fecha = new DateTime(2001, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        foreach (int? tiempo in new int?[] { 100, null })
            db.LogsAgenteIa.Add(new LogAgenteIaRow { Consulta = "Dato público de prueba", Modelo = "modelo-prueba-reportes", Estado = "OK", SessionId = Guid.NewGuid(), FechaHora = fecha, TiempoRespuestaMs = tiempo, TokensEntrada = 2, TokensSalida = 3 });
        await db.SaveChangesAsync(Ct);
        var calificaciones = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-11?eventoId={resena.EventoId}", Ct);
        Assert.Equal(5m, calificaciones.GetProperty("secciones")[0].GetProperty("totales")[0].GetProperty("promedio").GetDecimal());
        var estados = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-12?eventoId={resena.EventoId}", Ct);
        Assert.Equal(1, estados.GetProperty("secciones")[0].GetProperty("totales")[0].GetProperty("eventos").GetInt64());
        var carreras = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-13?eventoId={resena.EventoId}", Ct);
        Assert.Equal(0, carreras.GetProperty("secciones")[0].GetProperty("totalFilas").GetInt64());
        int categoriaCarrera = await db.CategoriasEvento.Where(c => c.Nombre == "Carrera").Select(c => c.Id).SingleAsync(Ct);
        await db.Eventos.Where(e => e.Id == resena.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoriaId, categoriaCarrera), Ct);
        await db.TiposEntrada.Where(t => t.EventoId == resena.EventoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Nombre, "5K"), Ct);
        var inscritos = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-13?eventoId={resena.EventoId}", Ct);
        var conteo = inscritos.GetProperty("secciones")[0].GetProperty("totales")[0];
        Assert.Equal(3, conteo.GetProperty("inscritos").GetDecimal());
        Assert.Equal(1, conteo.GetProperty("validados").GetDecimal());
        var ia = await admin.GetFromJsonAsync<JsonElement>("/api/v1/reportes/R-14?desde=2001-01-02&hasta=2001-01-03&estado=OK", Ct);
        var grupo = ia.GetProperty("secciones")[0];
        Assert.Equal(100m, grupo.GetProperty("filas")[0].GetProperty("tiempoPromedioMs").GetDecimal());
        Assert.Equal(2m, grupo.GetProperty("totales")[0].GetProperty("consultas").GetDecimal());
        Assert.Equal(JsonValueKind.Null, grupo.GetProperty("totales")[0].GetProperty("tiempoPromedioMs").ValueKind);
        var auditoria = await admin.GetFromJsonAsync<JsonElement>("/api/v1/reportes/R-15?pageSize=1", Ct);
        Assert.Equal(2, auditoria.GetProperty("secciones").GetArrayLength());
        Assert.False(ia.ToString().Contains("Dato público de prueba", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AsistenciaYLedgerSeDistinguenDeVentasBrutasTrasReembolso()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext();
        var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        int evento = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.EventoId).SingleAsync(Ct);
        foreach (var (codigo, campo, esperado) in new[] { ("R-06", "vendidos", 3m), ("R-07", "neto", 300m), ("R-08", "neto", 30m), ("R-09", "total", 369.60m) })
        {
            var reporte = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/{codigo}?eventoId={evento}", Ct);
            Assert.Equal(esperado, reporte.GetProperty("secciones")[0].GetProperty("totales")[0].GetProperty(campo).GetDecimal());
        }
        using var refund = await admin.PostAsync($"/api/v1/compras/{venta.PublicId}/reembolsar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);
        foreach (var (codigo, campo, esperado) in new[] { ("R-06", "anulados", 3m), ("R-07", "neto", 0m), ("R-08", "neto", 0m), ("R-09", "total", 369.60m) })
        {
            var reporte = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/{codigo}?eventoId={evento}", Ct);
            Assert.Equal(esperado, reporte.GetProperty("secciones")[0].GetProperty("totales")[0].GetProperty(campo).GetDecimal());
        }
        var fallidas = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-10?eventoId={evento}&estado=FALLIDA", Ct);
        Assert.Equal(0, fallidas.GetProperty("secciones")[0].GetProperty("totalFilas").GetInt64());
    }

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
        var compraFecha = await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.FechaCompra).SingleAsync(Ct);
        string dia = compraFecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var fueraDeRango = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/reportes/R-01?eventoId={evento}&hasta={dia}", Ct);
        Assert.Equal(0, fueraDeRango.GetProperty("secciones")[0].GetProperty("totalFilas").GetInt64());
        var swagger = await publico.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Ct);
        var content = swagger.GetProperty("paths").GetProperty("/api/v1/reportes/{codigo}").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content");
        Assert.True(content.TryGetProperty("application/json", out _));
        Assert.True(content.TryGetProperty("application/pdf", out _));
        Assert.True(content.TryGetProperty("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", out _));
    }
}
