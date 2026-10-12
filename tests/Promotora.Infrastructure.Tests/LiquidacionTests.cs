using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Finance;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class LiquidacionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private readonly byte[] qrKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task VeinteAdministradoresReintentanUnDebitoSinDuplicarSaldoNiAuditoria()
    {
        await using var db = postgres.CreateContext(); var (_, cuenta) = await VentaAsync(db);
        var admins = new List<int>(); for (int i = 0; i < 20; i++) admins.Add(await ReembolsoFixture.AdminAsync(db, Ct));
        var resultados = await Task.WhenAll(admins.Select(actor => EjecutarAsync(actor, new(cuenta, 100m, "misma-clave"))));
        Assert.All(resultados, r => Assert.True(r.IsSuccess)); Assert.Single(resultados.Select(r => r.Value).Distinct());
        var movimiento = Assert.Single(await db.MovimientosFinancieros.Where(m => m.CuentaId == cuenta && m.Tipo == "LIQUIDACION").ToArrayAsync(Ct));
        Assert.Equal(-100m, movimiento.Monto); Assert.Null(movimiento.CompraId); Assert.StartsWith("liquidacion:v1:", movimiento.Descripcion);
        Assert.DoesNotContain("misma-clave", movimiento.Descripcion!); Assert.Equal(DateTimeKind.Utc, movimiento.Fecha.Kind);
        Assert.Equal(200m, await SaldoAsync(db, cuenta));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "movimientos_financieros" && a.EntidadId == movimiento.Id.ToString() && a.Accion == "CREATE", Ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "cuentas_financieras" && a.EntidadId == cuenta.ToString() && a.Accion == "UPDATE", Ct));
        await ConciliarAsync(db, cuenta);
    }
    [Fact]
    public async Task DosClavesCompitenPorSaldoSinSobregiro()
    {
        await using var db = postgres.CreateContext(); var (_, cuenta) = await VentaAsync(db);
        int uno = await ReembolsoFixture.AdminAsync(db, Ct), dos = await ReembolsoFixture.AdminAsync(db, Ct);
        var resultados = await Task.WhenAll(EjecutarAsync(uno, new(cuenta, 200m, "uno")), EjecutarAsync(dos, new(cuenta, 200m, "dos")));
        Assert.Single(resultados, r => r.IsSuccess); Assert.Equal(ErrorType.Conflict, Assert.Single(resultados.Single(r => r.IsFailure).Errors).Type);
        Assert.Equal(100m, await SaldoAsync(db, cuenta)); await ConciliarAsync(db, cuenta);
    }
    [Fact]
    public async Task ClaveSeLimitaALaCuentaYCambioDeMontoEsConflicto()
    {
        await using var db = postgres.CreateContext(); var (_, a) = await VentaAsync(db); var (_, b) = await VentaAsync(db);
        int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        Assert.True((await EjecutarAsync(admin, new(a, 100m, "clave"))).IsSuccess);
        Assert.True((await EjecutarAsync(admin, new(b, 100m, "clave"))).IsSuccess);
        Assert.Equal("liquidacion.idempotencia", Assert.Single((await EjecutarAsync(admin, new(a, 101m, "clave"))).Errors).Code);
        Assert.Equal(200m, await SaldoAsync(db, a)); Assert.Equal(200m, await SaldoAsync(db, b));
    }
    [Fact]
    public async Task LiquidacionTotalPermiteCierreYRepeticionPosteriorSinEfectos()
    {
        await using var db = postgres.CreateContext(); var (_, cuenta) = await VentaAsync(db); int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        var primero = await EjecutarAsync(admin, new(cuenta, 300m, "total")); Assert.True(primero.IsSuccess);
        await using (var provider = Servicios(admin))
        await using (var scope = provider.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<ICommandHandler<CerrarCuentaCommand, CuentaDto>>().HandleAsync(new(cuenta), Ct)).IsSuccess);
        Assert.Equal(primero.Value, (await EjecutarAsync(admin, new(cuenta, 300m, "total"))).Value);
        Assert.True((await EjecutarAsync(admin, new(cuenta, 1m, "otra"))).IsFailure);
        Assert.Equal(0m, await SaldoAsync(db, cuenta)); await ConciliarAsync(db, cuenta);
    }
    [Theory]
    [InlineData("movimientos_financieros")]
    [InlineData("cuentas_financieras")]
    public async Task AuditoriaFallidaRevierteMovimientoSaldoYClave(string entidad)
    {
        await using var db = postgres.CreateContext(); var (_, cuenta) = await VentaAsync(db); int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => EjecutarAsync(admin, new(cuenta, 50m, "rollback"), entidad));
        Assert.Equal(300m, await SaldoAsync(db, cuenta)); Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CuentaId == cuenta && m.Tipo == "LIQUIDACION", Ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "cuentas_financieras" && a.EntidadId == cuenta.ToString() && a.Accion == "UPDATE", Ct));
        Assert.True((await EjecutarAsync(admin, new(cuenta, 50m, "rollback"))).IsSuccess); await ConciliarAsync(db, cuenta);
    }
    [Fact]
    public async Task SaldoDesconciliadoSeRechazaSinMovimiento()
    {
        await using var db = postgres.CreateContext(); var (_, cuenta) = await VentaAsync(db); int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        await db.CuentasFinancieras.Where(c => c.Id == cuenta).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, 301m), Ct);
        Assert.Equal("liquidacion.conciliacion", Assert.Single((await EjecutarAsync(admin, new(cuenta, 100m, "inconsistente"))).Errors).Code);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CuentaId == cuenta && m.Tipo == "LIQUIDACION", Ct));
    }
    [Fact]
    public async Task ReembolsoYRetiroConcurrentesMantienenLaConciliacion()
    {
        await using var db = postgres.CreateContext(); var (venta, cuenta) = await VentaAsync(db);
        int uno = await ReembolsoFixture.AdminAsync(db, Ct), dos = await ReembolsoFixture.AdminAsync(db, Ct);
        async Task<Result<ReembolsoDto>> ReembolsarAsync()
        {
            await using var provider = Servicios(dos); await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>().HandleAsync(new(venta.PublicId), Ct);
        }
        var retiro = EjecutarAsync(uno, new(cuenta, 100m, "carrera")); var reembolso = ReembolsarAsync();
        await Task.WhenAll(retiro, reembolso); Assert.NotEqual((await retiro).IsSuccess, (await reembolso).IsSuccess);
        Assert.Equal((await retiro).IsSuccess ? 200m : 0m, await SaldoAsync(db, cuenta)); await ConciliarAsync(db, cuenta);
    }

    private async Task<(VentaReembolso Venta, int Cuenta)> VentaAsync(PromotoraDbContext db)
    {
        var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, qrKey, false, Ct);
        int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
        return (venta, cuenta);
    }
    private async Task<Result<LiquidacionDto>> EjecutarAsync(int admin, LiquidarCuentaCommand command, string? fallar = null)
    {
        await using var provider = Servicios(admin, fallar); await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<LiquidarCuentaCommand, LiquidacionDto>>().HandleAsync(command, Ct);
    }
    private ServiceProvider Servicios(int admin, string? fallar = null)
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(postgres.ConnectionString); services.AddCuentasFinancieras(); services.AddReembolsos();
        services.AddScoped<ISesionRepository, EfSesionRepository>(); services.AddSingleton<IUsuarioActual>(new Actual(admin));
        if (fallar is not null) services.AddScoped<IAuditLogger>(sp => new AuditFallando(new EfAuditLogger(sp.GetRequiredService<PromotoraDbContext>(), Clock), fallar));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
    private static Task<decimal> SaldoAsync(PromotoraDbContext db, int cuenta) => db.CuentasFinancieras.Where(c => c.Id == cuenta).Select(c => c.Saldo).SingleAsync(Ct);
    private static async Task ConciliarAsync(PromotoraDbContext db, int cuenta) => Assert.Equal(await SaldoAsync(db, cuenta),
        await db.MovimientosFinancieros.Where(m => m.CuentaId == cuenta).SumAsync(m => m.Monto, Ct));
    private sealed record Actual(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
    private sealed class AuditFallando(IAuditLogger inner, string entidadFallo) : IAuditLogger
    {
        public Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, CancellationToken ct) => WriteAsync(usuario, accion, entidad, id, ip, null, ct);
        public async Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            await inner.WriteAsync(usuario, accion, entidad, id, ip, detalle, ct);
            if (entidad == entidadFallo) throw new InvalidOperationException("Fallo de auditoría de liquidación en prueba.");
        }
    }
}
