using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Finance;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;

namespace Promotora.Infrastructure.Tests;

public sealed class ReembolsoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    public async Task ReembolsoTotalRespetaUsadosAnuladosYRepeticion(int usados, bool anuladaAntes)
    {
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, key, false, Ct);
        int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        var boletos = await db.Boletos.AsNoTracking().Where(b => b.DetalleCompraId == venta.DetalleId).OrderBy(b => b.Id).ToArrayAsync(Ct);
        foreach (var boleto in boletos.Take(usados))
            await db.Boletos.Where(b => b.Id == boleto.Id && b.Estado == "VALIDO").ExecuteUpdateAsync(s => s.SetProperty(b => b.Estado, "USADO").SetProperty(b => b.FechaUso, Clock.UtcNow).SetProperty(b => b.ValidadoPor, admin), Ct);
        if (anuladaAntes)
        {
            long ultimo = boletos[^1].Id;
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            await db.Boletos.Where(b => b.Id == ultimo && b.Estado == "VALIDO").ExecuteUpdateAsync(s => s.SetProperty(b => b.Estado, "ANULADO"), Ct);
            await db.TiposEntrada.Where(t => t.Id == venta.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, t => t.CupoDisponible + 1), Ct);
            await tx.CommitAsync(Ct);
        }
        await using var provider = Servicios(admin); await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>();
        var uno = await handler.HandleAsync(new(venta.PublicId), Ct); Assert.True(uno.IsSuccess);
        var dos = await handler.HandleAsync(new(venta.PublicId), Ct); Assert.Equal(uno.Value, dos.Value);
        await VerificarAsync(db, venta, usados);
        var ids = boletos.Take(usados).Select(b => b.Id).ToArray();
        Assert.Equal(usados, await db.Boletos.CountAsync(b => ids.Contains(b.Id) && b.FechaUso != null && b.ValidadoPor == admin && b.Estado == "USADO", Ct));
    }

    [Fact]
    public async Task VeinteAdministradoresDistintosGeneranUnaSolaReversion()
    {
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, key, false, Ct);
        var actores = new List<int>(); for (int i = 0; i < 20; i++) actores.Add(await ReembolsoFixture.AdminAsync(db, Ct));
        var resultados = await Task.WhenAll(actores.Select(async actor =>
        {
            await using var provider = Servicios(actor); await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess)); await VerificarAsync(db, venta, 0);
    }

    [Fact]
    public async Task GratisAnulaYDevuelveCupoSinMovimientos()
    {
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, key, true, Ct);
        int admin = await ReembolsoFixture.AdminAsync(db, Ct); await using var provider = Servicios(admin); await using var scope = provider.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct);
        Assert.True(respuesta.IsSuccess); Assert.Equal(0m, respuesta.Value.MontoReembolsado); await VerificarAsync(db, venta, 0);
        Assert.Equal(respuesta.Value, (await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct)).Value);
    }

    [Fact]
    public async Task ComprasDistintasCompartenCuentasSinPerderSaldoEnReembolsosConcurrentes()
    {
        await using var db = postgres.CreateContext(); var ventas = new List<(VentaReembolso Venta, int Admin)>();
        for (int i = 0; i < 4; i++) ventas.Add((await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, key, false, Ct), await ReembolsoFixture.AdminAsync(db, Ct)));
        await Task.WhenAll(ventas.Select(async item =>
        {
            await using var provider = Servicios(item.Admin); await using var scope = provider.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(item.Venta.PublicId), Ct)).IsSuccess);
        }));
        foreach (var item in ventas) await VerificarAsync(db, item.Venta, 0);
    }

    [Theory]
    [InlineData("movimientos_financieros")]
    [InlineData("boletos")]
    [InlineData("pagos")]
    [InlineData("compras")]
    public async Task FalloDeAuditoriaRevierteTodoYPermiteReintento(string entidad)
    {
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, key, false, Ct);
        int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        await using (var provider = Servicios(admin, entidad))
        await using (var scope = provider.CreateAsyncScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct));
        Assert.Equal("APROBADO", await db.Pagos.Where(p => p.CompraId == venta.Id).Select(p => p.Estado).SingleAsync(Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == venta.Id && m.Tipo == "REEMBOLSO", Ct));
        Assert.Equal(3, await db.Boletos.CountAsync(b => b.DetalleCompraId == venta.DetalleId && b.Estado == "VALIDO", Ct));
        Assert.Equal(2, await db.TiposEntrada.Where(t => t.Id == venta.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == venta.Id.ToString() && a.Accion == "REFUND", Ct));
        await using var correcto = Servicios(admin); await using var nuevo = correcto.CreateAsyncScope();
        Assert.True((await nuevo.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct)).IsSuccess);
        await VerificarAsync(db, venta, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaldoInsuficienteOCuentaCerradaDevuelveConflictoSinEfectos(bool cerrada)
    {
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, key, false, Ct);
        int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
        decimal saldo = await db.CuentasFinancieras.Where(c => c.Id == cuenta).Select(c => c.Saldo).SingleAsync(Ct);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            db.MovimientosFinancieros.Add(new MovimientoFinancieroRow { CuentaId = cuenta, Tipo = "LIQUIDACION", Monto = -saldo, Fecha = Clock.UtcNow });
            await db.SaveChangesAsync(Ct);
            await db.CuentasFinancieras.Where(c => c.Id == cuenta).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, 0m), Ct);
            if (cerrada) await db.CuentasFinancieras.Where(c => c.Id == cuenta && c.Estado == "ACTIVA" && c.Saldo == 0m)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "CERRADA").SetProperty(c => c.FechaCierre, Clock.UtcNow), Ct);
            await tx.CommitAsync(Ct);
        }
        await using var provider = Servicios(admin); await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct);
        Assert.True(result.IsFailure); Assert.Equal(Promotora.Domain.Shared.ErrorType.Conflict, result.Errors[0].Type);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == venta.Id && m.Tipo == "REEMBOLSO", Ct));
        Assert.Equal("APROBADO", await db.Pagos.Where(p => p.CompraId == venta.Id).Select(p => p.Estado).SingleAsync(Ct));
        Assert.Equal(2, await db.TiposEntrada.Where(t => t.Id == venta.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
    }

    private ServiceProvider Servicios(int admin, string? fallar = null)
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(postgres.ConnectionString); services.AddReembolsos();
        services.AddScoped<ISesionRepository, EfSesionRepository>(); services.AddSingleton<IUsuarioActual>(new Actual(admin));
        if (fallar is not null) services.AddScoped<IAuditLogger>(sp => new AuditFallando(new EfAuditLogger(sp.GetRequiredService<PromotoraDbContext>(), Clock), fallar));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
    private static async Task VerificarAsync(PromotoraDbContext db, VentaReembolso venta, int usados)
    {
        Assert.Equal("COMPLETADA", await db.Compras.Where(c => c.Id == venta.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal("REEMBOLSADO", await db.Pagos.Where(p => p.CompraId == venta.Id).Select(p => p.Estado).SingleAsync(Ct));
        var ledger = await db.MovimientosFinancieros.AsNoTracking().Where(m => m.CompraId == venta.Id).ToArrayAsync(Ct);
        Assert.Equal(0m, ledger.Sum(m => m.Monto)); Assert.Equal(venta.Total == 0 ? 0 : 6, ledger.Length);
        foreach (var original in ledger.Where(m => m.Tipo != "REEMBOLSO")) Assert.Single(ledger, m => m.Tipo == "REEMBOLSO" && m.CuentaId == original.CuentaId && m.Monto == -original.Monto);
        Assert.Equal(5 - usados, await db.TiposEntrada.Where(t => t.Id == venta.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.Equal(3 - usados, await db.Boletos.CountAsync(b => b.DetalleCompraId == venta.DetalleId && b.Estado == "ANULADO", Ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == venta.Id.ToString() && a.Accion == "REFUND", Ct));
        Assert.False(await db.CuentasFinancieras.AnyAsync(c => c.Saldo != (db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), Ct));
    }
    private sealed record Actual(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
    private sealed class AuditFallando(IAuditLogger inner, string entidadFallo) : IAuditLogger
    {
        public Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, CancellationToken ct) => WriteAsync(usuario, accion, entidad, id, ip, null, ct);
        public async Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            await inner.WriteAsync(usuario, accion, entidad, id, ip, detalle, ct);
            if (entidad == entidadFallo) throw new InvalidOperationException("Fallo de auditoría de reembolso en prueba.");
        }
    }
}
