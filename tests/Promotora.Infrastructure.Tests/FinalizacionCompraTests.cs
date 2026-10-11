using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Finance;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class FinalizacionCompraTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task VeinteFinalizacionesConcurrentesConfirmanUnSoloCobroLedgerEmisionYOutbox()
    {
        var compra = await PrepararAsync();
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateContext(); return await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct);
        }));
        Assert.All(resultados, r => { Assert.True(r.IsSuccess, string.Join(",", r.Errors.Select(e => e.Code))); Assert.Equal(3, r.Value.Boletos); });
        await using var check = fixture.CreateContext(); await VerificarAsync(check, compra);
        var movimientoIds = await check.MovimientosFinancieros.Where(m => m.CompraId == compra.Id).Select(m => m.Id.ToString()).ToArrayAsync(Ct);
        Assert.Equal(3, await check.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "movimientos_financieros" && movimientoIds.Contains(a.EntidadId!), Ct));
        Assert.Equal(1, await check.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString() && a.Accion == "UPDATE", Ct));
        Assert.Equal(1, await check.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "pagos" && a.EntidadId == compra.IntentoId.ToString() && a.Accion == "PAYMENT", Ct));
    }

    [Fact]
    public async Task DiezComprasDistintasConCuentasCompartidasNoPierdenSaldo()
    {
        var primera = await PrepararAsync(); var compras = new List<Preparada> { primera };
        for (int i = 1; i < 10; i++) compras.Add(await PrepararAsync(compartir: primera));
        var resultados = await Task.WhenAll(compras.Select(async compra =>
        {
            await using var db = fixture.CreateContext(); return await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess));
        await using var check = fixture.CreateContext();
        foreach (var compra in compras) await VerificarAsync(check, compra);
        Assert.Equal(3000m, await check.CuentasFinancieras.Where(c => c.Tipo == "DUENO" && c.DuenoId == primera.DuenoId).Select(c => c.Saldo).SingleAsync(Ct));
        Assert.Equal(70, await check.TiposEntrada.Where(t => t.Id == primera.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
    }

    [Theory]
    [InlineData("pagos")]
    [InlineData("movimientos_financieros")]
    [InlineData("boletos")]
    [InlineData("compras")]
    [InlineData("outbox")]
    public async Task FalloEnCualquierEtapaReviertePagoLedgerSaldosEstadoEmisionYOutbox(string etapa)
    {
        var compra = await PrepararAsync(); await using var db = fixture.CreateContext();
        var saldos = await SaldosAsync(db);
        var audit = new AuditFallando(new EfAuditLogger(db, Clock), etapa);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Servicio(db, compra.CategoriaCarreraId, audit, etapa == "outbox" ? new OutboxFallando() : null)
            .CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct));
        await VerificarRollbackAsync(db, compra);
        Assert.Equal(saldos, await SaldosAsync(db));
        Assert.True((await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct)).IsSuccess);
        await VerificarAsync(db, compra);
    }

    [Fact]
    public async Task FirmaAusenteRevierteLaFinalizacionYCuentaCerradaNoSeReabre()
    {
        var compra = await PrepararAsync(); await using var db = fixture.CreateContext();
        var resultado = await Servicio(db, compra.CategoriaCarreraId, sinFirma: true).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct);
        Assert.Equal("boleto.configuracion", Assert.Single(resultado.Errors).Code); await VerificarRollbackAsync(db, compra);
        db.CuentasFinancieras.Add(new CuentaFinancieraRow { Tipo = "DUENO", DuenoId = compra.DuenoId, Moneda = "GTQ", Estado = "CERRADA", FechaCierre = Clock.UtcNow });
        await db.SaveChangesAsync(Ct);
        resultado = await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct);
        Assert.Equal("finanzas.cuenta", Assert.Single(resultado.Errors).Code); await VerificarRollbackAsync(db, compra);
        Assert.Equal("CERRADA", await db.CuentasFinancieras.Where(c => c.DuenoId == compra.DuenoId).Select(c => c.Estado).SingleAsync(Ct));
    }

    [Fact]
    public async Task SaldoCorruptoNoSeCompensaConLaSiguienteCompra()
    {
        var compra = await PrepararAsync(); await using var db = fixture.CreateContext();
        int plataforma = await db.CuentasFinancieras.Where(c => c.Tipo == "PLATAFORMA" && c.Moneda == "GTQ").Select(c => c.Id).SingleAsync(Ct);
        await db.CuentasFinancieras.Where(c => c.Id == plataforma).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, c => c.Saldo + 1m), Ct);
        try
        {
            var resultado = await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct);
            Assert.Equal("finanzas.saldo", Assert.Single(resultado.Errors).Code); await VerificarRollbackAsync(db, compra);
        }
        finally { await db.CuentasFinancieras.Where(c => c.Id == plataforma).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, c => c.Saldo - 1m), Ct); }
    }

    [Fact]
    public async Task CompraGratisNoGeneraMovimientosPeroSiPagoBoletosYConfirmacion()
    {
        var compra = await PrepararAsync(gratis: true); await using var db = fixture.CreateContext();
        var result = await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct);
        Assert.True(result.IsSuccess); await VerificarAsync(db, compra);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
    }

    [Fact]
    public async Task ResultadoAjenoRechazadoOEstadoPerdidoNoFinalizaCompra()
    {
        var compra = await PrepararAsync(); await using var db = fixture.CreateContext();
        foreach (var respuesta in new[] { Respuesta(compra) with { Referencia = Guid.NewGuid() }, Respuesta(compra) with { Estado = "RECHAZADO" }, Respuesta(compra) with { Monto = compra.Total + 1m } })
            Assert.True((await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, respuesta, [], Ct)).IsFailure);
        await VerificarRollbackAsync(db, compra);
        await db.Compras.Where(c => c.Id == compra.Id && c.Estado == "PROCESANDO").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "FALLIDA"), Ct);
        Assert.True((await Servicio(db, compra.CategoriaCarreraId).CompletarAsync(compra.PublicId, compra.IntentoId, Respuesta(compra), [], Ct)).IsFailure);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
    }

    [Fact]
    public void ServicioSeResuelveEnWorkerSinActorHttp()
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(fixture.ConnectionString);
        services.AddEmisionBoletos(_key); services.AddFinalizacionCompras();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope(); Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICompletarCompraPort>());
    }

    private CompletarCompraService Servicio(PromotoraDbContext db, int categoria, IAuditLogger? audit = null, IOutboxWriter? outbox = null, bool sinFirma = false)
    {
        audit ??= new EfAuditLogger(db, Clock); var uow = new EfUnitOfWork(db); var respaldo = new EfEmisionBoletosRepository(db, new(categoria));
        var emision = new EmisionBoletosService(respaldo, uow, new HmacTicketTokenService(sinFirma ? null : _key), Clock, audit);
        return new(new EfCobroRepository(db), new EfRegistroPagoRepository(db, Clock, audit), new EfCuentaRepository(db, audit),
            emision, respaldo, outbox ?? new EfOutboxWriter(db, Clock), uow, Clock, audit);
    }
    private static RespuestaPago Respuesta(Preparada c) => new(c.PublicId, c.Total, "GTQ", "SIMULADA", "APROBADO", Clock.UtcNow);
    private static Task<decimal[]> SaldosAsync(PromotoraDbContext db) => db.CuentasFinancieras.OrderBy(c => c.Id).Select(c => c.Saldo).ToArrayAsync(Ct);
    private static async Task VerificarRollbackAsync(PromotoraDbContext db, Preparada c)
    {
        Assert.Equal("PROCESANDO", await db.Compras.Where(x => x.Id == c.Id).Select(x => x.Estado).SingleAsync(Ct));
        var pago = await db.Pagos.AsNoTracking().SingleAsync(p => p.Id == c.IntentoId, Ct); Assert.Equal("PENDIENTE", pago.Estado); Assert.Null(pago.Respuesta);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == c.Id, Ct));
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == c.DetalleId, Ct));
        Assert.False(await db.OutboxMensajes.AnyAsync(o => o.AgregadoId == c.PublicId.ToString("D") && o.Tipo == "compra.completada", Ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "pagos" && a.EntidadId == c.IntentoId.ToString() && a.Accion == "PAYMENT", Ct));
    }
    private static async Task VerificarAsync(PromotoraDbContext db, Preparada c)
    {
        Assert.Equal("COMPLETADA", await db.Compras.Where(x => x.Id == c.Id).Select(x => x.Estado).SingleAsync(Ct));
        Assert.Equal(1, await db.Pagos.CountAsync(p => p.CompraId == c.Id && p.Estado == "APROBADO" && p.Monto == c.Total, Ct));
        var movimientos = await (from m in db.MovimientosFinancieros
                                 join cuenta in db.CuentasFinancieras on m.CuentaId equals cuenta.Id
                                 where m.CompraId == c.Id
                                 select new { m.Tipo, m.Monto, CuentaTipo = cuenta.Tipo, cuenta.DuenoId, cuenta.Moneda }).ToArrayAsync(Ct);
        Assert.Equal(c.Total, movimientos.Sum(m => m.Monto)); Assert.Equal(c.Total == 0m ? 0 : 3, movimientos.Length);
        if (c.Total > 0m)
        {
            var venta = Assert.Single(movimientos, m => m.Tipo == "VENTA"); Assert.Equal(300m, venta.Monto); Assert.Equal("DUENO", venta.CuentaTipo); Assert.Equal(c.DuenoId, venta.DuenoId);
            var comision = Assert.Single(movimientos, m => m.Tipo == "COMISION"); Assert.Equal(30m, comision.Monto); Assert.Equal("PLATAFORMA", comision.CuentaTipo);
            var iva = Assert.Single(movimientos, m => m.Tipo == "IMPUESTO"); Assert.Equal(39.60m, iva.Monto); Assert.Equal("IMPUESTOS", iva.CuentaTipo);
        }
        Assert.False(await db.CuentasFinancieras.AnyAsync(cuenta => cuenta.Saldo != (db.MovimientosFinancieros.Where(m => m.CuentaId == cuenta.Id).Sum(m => (decimal?)m.Monto) ?? 0m), Ct));
        var boletos = await db.Boletos.Where(b => b.DetalleCompraId == c.DetalleId).ToArrayAsync(Ct); Assert.Equal(3, boletos.Length); Assert.All(boletos, b => Assert.Equal("VALIDO", b.Estado));
        var outbox = await db.OutboxMensajes.SingleAsync(o => o.AgregadoId == c.PublicId.ToString("D") && o.Tipo == "compra.completada", Ct);
        Assert.Equal(c.PublicId, outbox.Payload.RootElement.GetProperty("publicId").GetGuid()); Assert.False(outbox.Publicado);
    }
    private async Task<Preparada> PrepararAsync(bool gratis = false, Preparada? compartir = null)
    {
        await using var db = fixture.CreateContext();
        var datos = compartir is null ? await CompraFixture.CrearAsync(db, 100, Ct) : (compartir.EventoId, compartir.TipoId, compartir.ClienteId);
        int categoriaCarrera = await db.CategoriasEvento.Where(c => c.Nombre == "Carrera").Select(c => c.Id).SingleAsync(Ct);
        int dueno = await (from e in db.Eventos join r in db.Recintos on e.RecintoId equals r.Id where e.Id == datos.Item1 select r.DuenoId).SingleAsync(Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.Item1, datos.Item2, 3, Ct));
        var importes = ImportesCompra.Calcular(gratis ? 0m : 300m, new(0.12m, 0.10m)).Value;
        var row = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.Item3,
            EventoId = datos.Item1,
            Estado = "PROCESANDO",
            FechaCompra = Clock.UtcNow,
            ExpiraEn = Clock.UtcNow.AddMinutes(15),
            ProcesandoDesde = Clock.UtcNow,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ",
            Subtotal = importes.Subtotal,
            ComisionPlataforma = importes.ComisionPlataforma,
            Impuestos = importes.Impuestos,
            Total = importes.Total
        };
        db.Compras.Add(row); await db.SaveChangesAsync(Ct);
        var detalle = new DetalleCompraRow { CompraId = row.Id, TipoEntradaId = datos.Item2, Cantidad = 3, PrecioUnitario = gratis ? 0m : 100m, Subtotal = importes.Subtotal };
        db.DetalleCompra.Add(detalle); await db.SaveChangesAsync(Ct);
        var intento = await new EfRegistroPagoRepository(db, Clock, new EfAuditLogger(db, Clock)).IniciarEnTransaccionAsync(row.PublicId, "SIMULADA", Ct);
        Assert.True(intento.IsSuccess); await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct);
        return new(row.Id, row.PublicId, intento.Value.Id, detalle.Id, categoriaCarrera, dueno, row.Total, datos.Item1, datos.Item2, datos.Item3);
    }
    private sealed record Preparada(int Id, Guid PublicId, long IntentoId, int DetalleId, int CategoriaCarreraId, int DuenoId, decimal Total, int EventoId, int TipoId, int ClienteId);
    private sealed class OutboxFallando : IOutboxWriter
    {
        public Task<long> EscribirAsync(NuevoMensajeOutbox mensaje, CancellationToken ct) => throw new InvalidOperationException("Fallo de outbox simulado.");
    }
    private sealed class AuditFallando(IAuditLogger inner, string entidadFallida) : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => WriteAsync(usuarioId, accion, entidad, entidadId, ip, null, ct);
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
            => entidad == entidadFallida ? throw new InvalidOperationException("Fallo de auditoría simulado.") : inner.WriteAsync(usuarioId, accion, entidad, entidadId, ip, detalle, ct);
    }
}
