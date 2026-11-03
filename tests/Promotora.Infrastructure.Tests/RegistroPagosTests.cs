using Microsoft.EntityFrameworkCore;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class RegistroPagosTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InicioYResultadoSeComponenEnLaTransaccionDelCallerYSeReviertenJuntos(bool revertir)
    {
        var compra = await CompraAsync(); await using var db = fixture.CreateContext();
        long intentoId = 0;
        var resultado = await new EfUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
        {
            var iniciado = await Repo(db).IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct);
            Assert.True(iniciado.IsSuccess); intentoId = iniciado.Value.Id;
            var resuelto = await Repo(db).ResolverEnTransaccionAsync(intentoId, Respuesta(compra), ct);
            Assert.True(resuelto.IsSuccess);
            return revertir ? Result<PagoRegistradoDto>.Failure(new Error("prueba.rollback", "Fallo posterior simulado.", ErrorType.Conflict)) : resuelto;
        }, Ct);
        Assert.Equal(!revertir, resultado.IsSuccess);
        Assert.Equal(revertir ? 0 : 1, await db.Pagos.CountAsync(p => p.CompraId == compra.Id && p.Estado == "APROBADO", Ct));
        Assert.Equal(revertir ? 0 : 2, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "pagos" && a.EntidadId == intentoId.ToString(), Ct));
        if (revertir) Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == compra.Id, Ct));
    }

    [Fact]
    public async Task VeinteIniciosYResultadosConcurrentesDejanUnPagoYUnaAuditoriaDeCadaOperacion()
    {
        var compra = await CompraAsync();
        var inicios = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            return await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct), Ct);
        }));
        Assert.All(inicios, r => Assert.True(r.IsSuccess)); Assert.Single(inicios.Select(r => r.Value.Id).Distinct());
        Assert.Single(inicios.Select(r => r.Value.Fecha).Distinct());
        long id = inicios[0].Value.Id; var respuesta = Respuesta(compra);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            return await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).ResolverEnTransaccionAsync(id, respuesta, ct), Ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess));
        await using var check = fixture.CreateContext();
        var pago = await check.Pagos.AsNoTracking().SingleAsync(p => p.CompraId == compra.Id, Ct);
        Assert.Equal("APROBADO", pago.Estado); Assert.Equal(compra.Total, pago.Monto); Assert.Equal(compra.PublicId.ToString("D"), pago.Referencia);
        Assert.Null(pago.Metodo); Assert.Equal("APROBADO", pago.Respuesta!.RootElement.GetProperty("estado").GetString());
        Assert.Equal(1, await check.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "pagos" && a.EntidadId == id.ToString() && a.Accion == "CREATE", Ct));
        Assert.Equal(1, await check.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "pagos" && a.EntidadId == id.ToString() && a.Accion == "PAYMENT", Ct));
        Assert.Equal("PROCESANDO", await check.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.False(await check.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
        // Un contexto nuevo conserva el resultado; un rechazo tardío no lo sobrescribe.
        var repetido = await new EfUnitOfWork(check).ExecuteInTransactionAsync(ct => Repo(check).IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct), Ct);
        Assert.Equal(id, repetido.Value.Id);
        var contradictorio = await new EfUnitOfWork(check).ExecuteInTransactionAsync(ct => Repo(check).ResolverEnTransaccionAsync(id, respuesta with { Estado = "RECHAZADO" }, ct), Ct);
        Assert.True(contradictorio.IsFailure);
    }

    [Fact]
    public async Task IntentoRechazadoPermaneceYElSiguienteTieneOtroId()
    {
        var compra = await CompraAsync(); await using var db = fixture.CreateContext();
        var primero = await IniciarAsync(db, compra);
        Assert.True((await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).ResolverEnTransaccionAsync(primero.Id, Respuesta(compra, "RECHAZADO"), ct), Ct)).IsSuccess);
        var segundo = await IniciarAsync(db, compra);
        Assert.NotEqual(primero.Id, segundo.Id); Assert.Equal("PENDIENTE", segundo.Estado);
        Assert.Equal(2, await db.Pagos.CountAsync(p => p.CompraId == compra.Id, Ct));
        Assert.Equal("RECHAZADO", await db.Pagos.Where(p => p.Id == primero.Id).Select(p => p.Estado).SingleAsync(Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FalloDeAuditoriaRevierteAltaOResultado(bool alta)
    {
        var compra = await CompraAsync(); await using var db = fixture.CreateContext();
        var inicial = alta ? null : await IniciarAsync(db, compra);
        var repo = new EfRegistroPagoRepository(db, Clock, new AuditFallando());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfUnitOfWork(db).ExecuteInTransactionAsync(
            ct => alta ? repo.IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct) : repo.ResolverEnTransaccionAsync(inicial!.Id, Respuesta(compra), ct), Ct));
        if (alta) Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == compra.Id, Ct));
        else
        {
            var row = await db.Pagos.AsNoTracking().SingleAsync(p => p.Id == inicial!.Id, Ct);
            Assert.Equal("PENDIENTE", row.Estado); Assert.Null(row.Respuesta);
        }
    }

    [Fact]
    public async Task RespuestasIncompatiblesNoSeRegistranYNoExistenOperacionesSinTransaccion()
    {
        var compra = await CompraAsync(); await using var db = fixture.CreateContext(); var pago = await IniciarAsync(db, compra);
        foreach (var respuesta in new[] { Respuesta(compra) with { Monto = compra.Total + 1m }, Respuesta(compra) with { Moneda = "USD" },
            Respuesta(compra) with { Referencia = Guid.NewGuid() }, Respuesta(compra) with { Pasarela = "OTRA" },
            Respuesta(compra) with { Estado = "REEMBOLSADO" }, Respuesta(compra) with { Fecha = new DateTime(2030, 1, 1) } })
        {
            var result = await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).ResolverEnTransaccionAsync(pago.Id, respuesta, ct), Ct);
            Assert.True(result.IsFailure);
        }
        Assert.Equal("PENDIENTE", await db.Pagos.Where(p => p.Id == pago.Id).Select(p => p.Estado).SingleAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo(db).IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", Ct));
    }

    [Fact]
    public async Task SoloProcesandoIniciaPagoYNoSeResuelveTrasPerderLaTransicion()
    {
        var compra = await CompraAsync(); await using var db = fixture.CreateContext();
        var pago = await IniciarAsync(db, compra);
        await db.Compras.Where(c => c.Id == compra.Id && c.Estado == "PROCESANDO").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "FALLIDA"), Ct);
        var resultado = await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).ResolverEnTransaccionAsync(pago.Id, Respuesta(compra), ct), Ct);
        Assert.True(resultado.IsFailure);
        Assert.Equal("PENDIENTE", await db.Pagos.Where(p => p.Id == pago.Id).Select(p => p.Estado).SingleAsync(Ct));
        var retomado = await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct), Ct);
        Assert.True(retomado.IsFailure);
        var otra = await CompraAsync(estado: "PENDIENTE");
        var inicio = await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).IniciarEnTransaccionAsync(otra.PublicId, "SIMULADA", ct), Ct);
        Assert.True(inicio.IsFailure); Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == otra.Id, Ct));
    }

    [Fact]
    public async Task CompraGratuitaRegistraPagoCeroSinMovimientos()
    {
        var compra = await CompraAsync(0m); await using var db = fixture.CreateContext();
        var pago = await IniciarAsync(db, compra);
        var result = await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).ResolverEnTransaccionAsync(pago.Id, Respuesta(compra), ct), Ct);
        Assert.True(result.IsSuccess); Assert.Equal(0m, result.Value.Monto);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
    }

    private static EfRegistroPagoRepository Repo(PromotoraDbContext db) => new(db, Clock, new EfAuditLogger(db, Clock));
    private static RespuestaPago Respuesta(CompraRow c, string estado = "APROBADO") => new(c.PublicId, c.Total, c.Moneda, "SIMULADA", estado, Clock.UtcNow);
    private static async Task<PagoRegistradoDto> IniciarAsync(PromotoraDbContext db, CompraRow compra)
    {
        var result = await new EfUnitOfWork(db).ExecuteInTransactionAsync(ct => Repo(db).IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", ct), Ct);
        Assert.True(result.IsSuccess); return result.Value;
    }
    private async Task<CompraRow> CompraAsync(decimal total = 123.20m, string estado = "PROCESANDO")
    {
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, Ct);
        var row = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            FechaCompra = Clock.UtcNow,
            Estado = estado,
            ExpiraEn = Clock.UtcNow.AddMinutes(15),
            ProcesandoDesde = estado == "PROCESANDO" ? Clock.UtcNow : null,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ",
            Subtotal = total == 0m ? 0m : 100m,
            ComisionPlataforma = total == 0m ? 0m : 10m,
            Impuestos = total == 0m ? 0m : 13.20m,
            Total = total
        };
        db.Compras.Add(row); await db.SaveChangesAsync(Ct); return row;
    }
    private sealed class AuditFallando : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría de prueba.");
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría de prueba.");
    }
}
