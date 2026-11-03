using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class EmisionBoletosTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task VeinteEmisionesConcurrentesCreanUnBoletoPorUnidadYUnaAuditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(true, false, "COMPLETADA", true, ct);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            return await Servicio(db, compra.CategoriaCarreraId).EmitirAsync(compra.PublicId, [], ct);
        }));
        Assert.All(resultados, r => { Assert.True(r.IsSuccess); Assert.Equal(3, r.Value.Count); });
        var primeros = resultados[0].Value;
        Assert.Equal(3, primeros.Select(b => b.PublicId).Distinct().Count());
        var tokens = new HmacTicketTokenService(_key);
        foreach (var r in resultados)
        {
            Assert.True(primeros.Select(b => b.PublicId).SequenceEqual(r.Value.Select(b => b.PublicId)));
            foreach (var boleto in r.Value) { Assert.True(tokens.Validar(boleto.CodigoQr, out var id)); Assert.Equal(boleto.PublicId, id); }
        }
        await using var verificacion = fixture.CreateContext();
        Assert.Equal(3, await verificacion.Boletos.CountAsync(b => b.DetalleCompraId == compra.DetalleId, ct));
        var ids = primeros.Select(b => b.Id.ToString()).ToArray();
        Assert.Equal(3, await verificacion.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "boletos" && ids.Contains(a.EntidadId!), ct));
    }

    [Theory]
    [InlineData("PENDIENTE", "boleto.estado")]
    [InlineData("PROCESANDO", "boleto.estado")]
    [InlineData("COMPLETADA", "boleto.finanzas")]
    public async Task NoEmiteSinEstadoCompletadoORegistroFinanciero(string estado, string codigo)
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(false, false, estado, false, ct);
        await using var db = fixture.CreateContext();
        var resultado = await Servicio(db, compra.CategoriaCarreraId).EmitirAsync(compra.PublicId, [], ct);
        Assert.Equal(codigo, Assert.Single(resultado.Errors).Code);
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, ct));
    }

    [Fact]
    public async Task CarreraExigeDatosPorUnidadYReintentoConservaLosOriginales()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(true, true, "COMPLETADA", true, ct);
        await using var db = fixture.CreateContext(); var servicio = Servicio(db, compra.CategoriaCarreraId);
        Assert.True((await servicio.EmitirAsync(compra.PublicId, [], ct)).IsFailure);
        var insuficientes = new AsistentesDetalle(compra.DetalleId, [new("Asistente", "Documento")]);
        Assert.True((await servicio.EmitirAsync(compra.PublicId, [insuficientes], ct)).IsFailure);
        var personas = new AsistentesDetalle(compra.DetalleId, Enumerable.Range(1, 3).Select(i => new DatosAsistente($" Asistente {i} ", $"Doc {i}", "{\"talla\":\"M\"}")).ToArray());
        var emitidos = await servicio.EmitirAsync(compra.PublicId, [personas], ct);
        Assert.True(emitidos.IsSuccess);
        Assert.Equal("Asistente 1", emitidos.Value[0].Asistente.NombreAsistente);
        Assert.All(emitidos.Value, b => Assert.Equal("{\"talla\":\"M\"}", b.Asistente.DatosExtraJson));
        var repetidos = await servicio.EmitirAsync(compra.PublicId, [], ct);
        Assert.True(repetidos.IsSuccess); Assert.True(emitidos.Value[0].CodigoQr == repetidos.Value[0].CodigoQr);
        Assert.Equal("Asistente 1", repetidos.Value[0].Asistente.NombreAsistente);
    }

    [Fact]
    public async Task FalloDeAuditoriaRevierteTodosLosBoletos()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(true, false, "COMPLETADA", true, ct);
        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Servicio(db, compra.CategoriaCarreraId, new AuditFallando()).EmitirAsync(compra.PublicId, [], ct));
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, ct));
        Assert.True((await Servicio(db, compra.CategoriaCarreraId).EmitirAsync(compra.PublicId, [], ct)).IsSuccess);
    }

    [Fact]
    public async Task PagoSinLedgerNoAutorizaEmisionYRegistroCorrectoSi()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(false, false, "COMPLETADA", true, ct);
        await using var db = fixture.CreateContext();
        Assert.Equal("boleto.finanzas", Assert.Single((await Servicio(db, compra.CategoriaCarreraId).EmitirAsync(compra.PublicId, [], ct)).Errors).Code);
        var correcta = await PrepararAsync(false, false, "COMPLETADA", true, ct, registrarLedger: true);
        Assert.True((await Servicio(db, correcta.CategoriaCarreraId).EmitirAsync(correcta.PublicId, [], ct)).IsSuccess);
    }

    [Fact]
    public async Task FirmaNoConfiguradaYOperacionSinTransaccionNoPersisten()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(true, false, "COMPLETADA", true, ct);
        await using var db = fixture.CreateContext();
        var service = new EmisionBoletosService(new EfEmisionBoletosRepository(db, new(compra.CategoriaCarreraId)), new EfUnitOfWork(db), new HmacTicketTokenService(null),
            new SystemClock(TimeProvider.System), new EfAuditLogger(db, new SystemClock(TimeProvider.System)));
        Assert.Equal("boleto.configuracion", Assert.Single((await service.EmitirAsync(compra.PublicId, [], ct)).Errors).Code);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnTransaccionAsync(compra.PublicId, [], ct));
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, ct));
    }

    [Fact]
    public void EmisionSeResuelveEnWorkerSinActorHttp()
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(fixture.ConnectionString); services.AddEmisionBoletos(_key);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope(); Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmisionBoletosPort>());
    }

    [Fact]
    public async Task CategoriaRenombradaMantieneExigenciaYConfiguracionAusenteFallaCerrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(true, true, "COMPLETADA", true, ct);
        await using var db = fixture.CreateContext();
        await db.CategoriasEvento.Where(c => c.Id == compra.CategoriaCarreraId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Nombre, "Carrera renombrada"), ct);
        try
        {
            Assert.Equal("boleto.asistentes", Assert.Single((await Servicio(db, compra.CategoriaCarreraId).EmitirAsync(compra.PublicId, [], ct)).Errors).Code);
            var clock = new SystemClock(TimeProvider.System);
            var sinCategoria = new EmisionBoletosService(new EfEmisionBoletosRepository(db, new()), new EfUnitOfWork(db), new HmacTicketTokenService(_key), clock, new EfAuditLogger(db, clock));
            Assert.Equal("boleto.categoria", Assert.Single((await sinCategoria.EmitirAsync(compra.PublicId, [], ct)).Errors).Code);
            Assert.Equal("boleto.categoria", Assert.Single((await Servicio(db, int.MaxValue).EmitirAsync(compra.PublicId, [], ct)).Errors).Code);
            Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, ct));
        }
        finally
        {
            await db.CategoriasEvento.Where(c => c.Id == compra.CategoriaCarreraId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Nombre, "Carrera"), ct);
        }
    }

    [Fact]
    public async Task SaldoInconsistenteImpideEmisionAunqueComponentesCuadren()
    {
        var ct = TestContext.Current.CancellationToken;
        var compra = await PrepararAsync(false, false, "COMPLETADA", true, ct, registrarLedger: true);
        await using var db = fixture.CreateContext();
        int cuenta = await db.MovimientosFinancieros.Where(m => m.Tipo == "VENTA" && db.Compras.Any(c => c.Id == m.CompraId && c.PublicId == compra.PublicId)).Select(m => m.CuentaId).SingleAsync(ct);
        // Corrupción simulada en la base temporal: demuestra que el guard rechaza el desbalance.
        await db.CuentasFinancieras.Where(c => c.Id == cuenta).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, 0m), ct);
        Assert.Equal("boleto.finanzas", Assert.Single((await Servicio(db, compra.CategoriaCarreraId).EmitirAsync(compra.PublicId, [], ct)).Errors).Code);
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, ct));
    }

    private EmisionBoletosService Servicio(PromotoraDbContext db, int categoriaCarreraId, IAuditLogger? audit = null)
    {
        var clock = new SystemClock(TimeProvider.System);
        return new(new EfEmisionBoletosRepository(db, new(categoriaCarreraId)), new EfUnitOfWork(db), new HmacTicketTokenService(_key), clock, audit ?? new EfAuditLogger(db, clock));
    }

    private async Task<(Guid PublicId, int DetalleId, int CategoriaCarreraId)> PrepararAsync(bool gratis, bool carrera, string estado, bool pago, CancellationToken ct, bool registrarLedger = false)
    {
        await using var db = fixture.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, ct);
        if (carrera)
        {
            int categoria = await db.CategoriasEvento.Where(c => c.Nombre == "Carrera").Select(c => c.Id).SingleAsync(ct);
            await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoriaId, categoria), ct);
        }
        // Fixture de una compra existente; estos datos financieros no son implementación de F7.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 3, ct));
        var importes = ImportesCompra.Calcular(gratis ? 0m : 300m, new(0.12m, 0.10m)).Value;
        var c = new CompraRow
        {
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            Estado = estado,
            PublicId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ",
            ExpiraEn = DateTime.UtcNow.AddMinutes(15),
            Subtotal = importes.Subtotal,
            ComisionPlataforma = importes.ComisionPlataforma,
            Impuestos = importes.Impuestos,
            Total = importes.Total
        };
        db.Compras.Add(c); await db.SaveChangesAsync(ct);
        var detalle = new DetalleCompraRow { CompraId = c.Id, TipoEntradaId = datos.TipoId, Cantidad = 3, PrecioUnitario = gratis ? 0m : 100m, Subtotal = importes.Subtotal };
        db.DetalleCompra.Add(detalle); await db.SaveChangesAsync(ct);
        if (pago) db.Pagos.Add(new PagoRow { CompraId = c.Id, Pasarela = "SIMULADA", Referencia = c.PublicId.ToString(), Monto = c.Total, Estado = "APROBADO" });
        if (registrarLedger)
        {
            int dueno = await (from e in db.Eventos join r in db.Recintos on e.RecintoId equals r.Id where e.Id == c.EventoId select r.DuenoId).SingleAsync(ct);
            var cuenta = new CuentaFinancieraRow { DuenoId = dueno, Tipo = "DUENO", Moneda = "GTQ" }; db.CuentasFinancieras.Add(cuenta); await db.SaveChangesAsync(ct);
            int plataforma = await db.CuentasFinancieras.Where(a => a.Tipo == "PLATAFORMA" && a.Moneda == "GTQ").Select(a => a.Id).SingleAsync(ct);
            int impuestos = await db.CuentasFinancieras.Where(a => a.Tipo == "IMPUESTOS" && a.Moneda == "GTQ").Select(a => a.Id).SingleAsync(ct);
            foreach (var (id, tipo, monto) in new[] { (cuenta.Id, "VENTA", c.Subtotal), (plataforma, "COMISION", c.ComisionPlataforma), (impuestos, "IMPUESTO", c.Impuestos) })
            {
                db.MovimientosFinancieros.Add(new MovimientoFinancieroRow { CuentaId = id, CompraId = c.Id, Tipo = tipo, Monto = monto });
                await db.CuentasFinancieras.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Saldo, a => a.Saldo + monto), ct);
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (c.PublicId, detalle.Id, await db.CategoriasEvento.Where(cat => cat.Nombre == "Carrera").Select(cat => cat.Id).SingleAsync(ct));
    }

    private sealed class AuditFallando : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => throw new InvalidOperationException("Fallo de prueba.");
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => throw new InvalidOperationException("Fallo de prueba.");
    }
}
