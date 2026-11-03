using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class ProcesarCompraTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VeinteDuplicadosConcurrentesCompletanUnaSolaCompraConAsistentes(bool carrera)
    {
        var compra = await PrepararAsync(carrera);
        var gateway = new FakePaymentGateway(new(), Clock);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            return await Servicio(db, gateway).ProcesarAsync(compra.Mensaje, Ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess, string.Join(",", r.Errors.Select(e => e.Code))));
        await using var check = fixture.CreateContext();
        Assert.Equal("COMPLETADA", await check.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal(1, await check.Pagos.CountAsync(p => p.CompraId == compra.Id && p.Estado == "APROBADO", Ct));
        Assert.Equal(246.40m, await check.MovimientosFinancieros.Where(m => m.CompraId == compra.Id).SumAsync(m => m.Monto, Ct));
        var boletos = await check.Boletos.Where(b => b.DetalleCompraId == compra.DetalleId).ToArrayAsync(Ct);
        Assert.Equal(2, boletos.Length);
        Assert.All(boletos, b => Assert.True(new HmacTicketTokenService(_key).Validar(b.CodigoQr, out _)));
        if (carrera) Assert.Equal(new[] { "Persona 1", "Persona 2" }, boletos.Select(b => b.NombreAsistente).Order().ToArray());
        Assert.Equal(1, await check.MensajesProcesados.CountAsync(m => m.MensajeId == compra.Mensaje.MensajeId, Ct));
        Assert.Equal(1, await check.OutboxMensajes.CountAsync(o => o.AgregadoId == compra.Mensaje.PublicId.ToString("D") && o.Tipo == "compra.completada", Ct));
        Assert.Equal(3, await check.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await check.CuentasFinancieras.AnyAsync(c => c.Saldo != (check.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), Ct));
    }

    [Fact]
    public async Task RechazoDevuelveCupoExactamenteUnaVezYNoGeneraBoletosNiLedger()
    {
        var compra = await PrepararAsync(); var gateway = new FakePaymentGateway(new(1m), Clock);
        await using var db = fixture.CreateContext();
        Assert.True((await Servicio(db, gateway).ProcesarAsync(compra.Mensaje, Ct)).IsSuccess);
        Assert.True((await Servicio(db, gateway).ProcesarAsync(compra.Mensaje, Ct)).IsSuccess);
        Assert.Equal("FALLIDA", await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.Equal(1, await db.Pagos.CountAsync(p => p.CompraId == compra.Id && p.Estado == "RECHAZADO", Ct));
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
    }

    [Fact]
    public async Task FalloDeEmisionRevierteTomaDedupPagoLedgerYCuentasYPermiteReintentar()
    {
        var compra = await PrepararAsync(); var gateway = new FakePaymentGateway(new(), Clock);
        await using var db = fixture.CreateContext();
        var result = await Servicio(db, gateway, sinFirma: true).ProcesarAsync(compra.Mensaje, Ct);
        Assert.True(result.IsFailure);
        Assert.Equal("PENDIENTE", await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Null(await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.ProcesandoDesde).SingleAsync(Ct));
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == compra.Mensaje.MensajeId, Ct));
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == compra.Id, Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
        Assert.True((await Servicio(db, gateway).ProcesarAsync(compra.Mensaje, Ct)).IsSuccess);
    }

    [Fact]
    public async Task CanceladaSeOmiteYUnPublicIdAjenoAlOutboxSeRechazaSinCobrar()
    {
        var compra = await PrepararAsync(); var gateway = new FakePaymentGateway(new(), Clock);
        await using var db = fixture.CreateContext();
        var falso = compra.Mensaje with { PublicId = Guid.NewGuid() };
        Assert.True((await Servicio(db, gateway).ProcesarAsync(falso, Ct)).IsFailure);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            Assert.True(await new EfCupoEntradaRepository(db).FinalizarYDevolverAsync(compra.Id, EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, Clock.UtcNow, Ct));
            await tx.CommitAsync(Ct);
        }
        Assert.True((await Servicio(db, gateway).ProcesarAsync(compra.Mensaje, Ct)).IsSuccess);
        Assert.Null(await gateway.ConsultarAsync(compra.Mensaje.PublicId, Ct));
        Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
    }

    private ProcesarCompraService Servicio(PromotoraDbContext db, FakePaymentGateway gateway, bool sinFirma = false)
    {
        var uow = new EfUnitOfWork(db); var audit = new EfAuditLogger(db, Clock);
        var pagos = new EfRegistroPagoRepository(db, Clock, audit); var respaldo = new EfEmisionBoletosRepository(db, new(5));
        var emision = new EmisionBoletosService(respaldo, uow, new HmacTicketTokenService(sinFirma ? null : _key), Clock, audit);
        var completar = new CompletarCompraService(new EfCobroRepository(db), pagos, new EfCuentaRepository(db, audit),
            emision, respaldo, new EfOutboxWriter(db, Clock), uow, Clock, audit);
        return new(new EfCompraMensajeRepository(db), new EfMensajeProcesadoRepository(db, Clock), new EfCompraTomaRepository(db, Clock, audit),
            pagos, gateway, completar, new EfCupoEntradaRepository(db), uow, Clock, audit);
    }

    private async Task<Preparada> PrepararAsync(bool carrera = false)
    {
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        if (carrera) await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoriaId, 5), Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 2, Ct));
        var row = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            Estado = "PENDIENTE",
            FechaCompra = Clock.UtcNow,
            ExpiraEn = Clock.UtcNow.AddMinutes(15),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ",
            Subtotal = 200m,
            ComisionPlataforma = 20m,
            Impuestos = 26.40m,
            Total = 246.40m
        };
        db.Compras.Add(row); await db.SaveChangesAsync(Ct);
        var detalle = new DetalleCompraRow { CompraId = row.Id, TipoEntradaId = datos.TipoId, Cantidad = 2, PrecioUnitario = 100m, Subtotal = 200m };
        db.DetalleCompra.Add(detalle); await db.SaveChangesAsync(Ct);
        var personas = new[] { new DatosAsistente("Persona 1", "DOC1"), new DatosAsistente("Persona 2", "DOC2") };
        var payload = JsonSerializer.SerializeToElement(new { publicId = row.PublicId, asistentes = carrera ? new[] { new { tipoEntradaId = datos.TipoId, personas } } : [] });
        long id = await new EfOutboxWriter(db, Clock).EscribirAsync(new("Compra", row.PublicId.ToString("D"), TiposMensaje.ProcesarCompra, payload), Ct);
        await tx.CommitAsync(Ct);
        return new(row.Id, detalle.Id, datos.TipoId, new(id, row.PublicId));
    }
    private sealed record Preparada(int Id, int DetalleId, int TipoId, MensajeCompraDto Mensaje);
}
