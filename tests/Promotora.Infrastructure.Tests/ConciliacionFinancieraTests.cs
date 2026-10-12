using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Finance;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class ConciliacionFinancieraTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly byte[] qrKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task VentasReembolsoRetirosReintentosYCierreCuadranEnCadaPaso()
    {
        await using var db = postgres.CreateContext(); int admin = await ReembolsoFixture.AdminAsync(db, Ct);
        decimal inicial = await db.CuentasFinancieras.SumAsync(c => c.Saldo, Ct);
        decimal comisionInicial = await SistemaAsync(db, "PLATAFORMA"), impuestoInicial = await SistemaAsync(db, "IMPUESTOS");
        var uno = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, qrKey, false, Ct); await ConciliarAsync(db, inicial + 369.60m);
        var dos = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, qrKey, false, Ct); await ConciliarAsync(db, inicial + 739.20m);
        var gratis = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, qrKey, true, Ct); await ConciliarAsync(db, inicial + 739.20m);
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == gratis.Id, Ct));
        int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == dos.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
        await using var provider = Servicios(admin); await using var scope = provider.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var reembolsar = sp.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>();
        var liquidar = sp.GetRequiredService<ICommandHandler<LiquidarCuentaCommand, LiquidacionDto>>();
        var cerrar = sp.GetRequiredService<ICommandHandler<CerrarCuentaCommand, CuentaDto>>();
        Assert.True((await reembolsar.HandleAsync(new(uno.PublicId), Ct)).IsSuccess); await ConciliarAsync(db, inicial + 369.60m);
        Assert.True((await reembolsar.HandleAsync(new(uno.PublicId), Ct)).IsSuccess); await ConciliarAsync(db, inicial + 369.60m);
        var parcial = await liquidar.HandleAsync(new(cuenta, 100m, "retiro-uno"), Ct); Assert.True(parcial.IsSuccess); await ConciliarAsync(db, inicial + 269.60m);
        Assert.Equal(parcial.Value, (await liquidar.HandleAsync(new(cuenta, 100m, "retiro-uno"), Ct)).Value); await ConciliarAsync(db, inicial + 269.60m);
        Assert.True((await liquidar.HandleAsync(new(cuenta, 200m, "retiro-dos"), Ct)).IsSuccess); await ConciliarAsync(db, inicial + 69.60m);
        Assert.True((await cerrar.HandleAsync(new(cuenta), Ct)).IsSuccess); await ConciliarAsync(db, inicial + 69.60m);
        Assert.Equal(parcial.Value, (await liquidar.HandleAsync(new(cuenta, 100m, "retiro-uno"), Ct)).Value);
        Assert.True((await liquidar.HandleAsync(new(cuenta, 1m, "cerrada"), Ct)).IsFailure); await ConciliarAsync(db, inicial + 69.60m);
        var estado = await sp.GetRequiredService<IQueryHandler<ObtenerCuentaQuery, CuentaDto>>().HandleAsync(new(cuenta), Ct);
        Assert.Equal("CERRADA", estado.Value.Estado); Assert.Equal(0m, estado.Value.Saldo);
        var extracto = await sp.GetRequiredService<IQueryHandler<MovimientosCuentaQuery, Promotora.Application.Shared.PaginaDto<MovimientoCuentaDto>>>()
            .HandleAsync(new(cuenta), Ct);
        Assert.Equal(3, extracto.Value.Total); Assert.Equal(0m, extracto.Value.Items.Sum(m => m.Monto));
        Assert.Equal(comisionInicial + 30m, await SistemaAsync(db, "PLATAFORMA"));
        Assert.Equal(impuestoInicial + 39.60m, await SistemaAsync(db, "IMPUESTOS"));
        Assert.Equal(2, await db.MovimientosFinancieros.CountAsync(m => m.CuentaId == cuenta && m.Tipo == "LIQUIDACION", Ct));
    }
    [Fact]
    public async Task ReembolsosYRetirosDeSeisVentasConcurrentesConciliadosEnTodasLasCuentas()
    {
        await using var db = postgres.CreateContext(); var ventas = new List<(VentaReembolso Venta, int Cuenta, int Admin)>();
        decimal inicial = await db.CuentasFinancieras.SumAsync(c => c.Saldo, Ct);
        decimal comisionInicial = await SistemaAsync(db, "PLATAFORMA"), impuestoInicial = await SistemaAsync(db, "IMPUESTOS");
        for (int i = 0; i < 6; i++)
        {
            var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, qrKey, false, Ct);
            int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
            ventas.Add((venta, cuenta, await ReembolsoFixture.AdminAsync(db, Ct)));
        }
        await ConciliarAsync(db, inicial + 2217.60m);
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tareas = ventas.Select(async (item, indice) =>
        {
            await using var provider = Servicios(item.Admin); await using var scope = provider.CreateAsyncScope();
            await salida.Task.WaitAsync(Ct);
            if (indice % 2 == 0)
            {
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>>();
                Assert.True((await handler.HandleAsync(new(item.Venta.PublicId), Ct)).IsSuccess);
                Assert.True((await handler.HandleAsync(new(item.Venta.PublicId), Ct)).IsSuccess);
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<LiquidarCuentaCommand, LiquidacionDto>>();
                var comando = new LiquidarCuentaCommand(item.Cuenta, 300m, "concurrente");
                var primero = await handler.HandleAsync(comando, Ct); Assert.True(primero.IsSuccess);
                Assert.Equal(primero.Value, (await handler.HandleAsync(comando, Ct)).Value);
            }
        }).ToArray();
        salida.SetResult(); await Task.WhenAll(tareas); await ConciliarAsync(db, inicial + 208.80m);
        int[] cuentas = ventas.Select(v => v.Cuenta).ToArray(), compras = ventas.Select(v => v.Venta.Id).ToArray();
        Assert.Equal(3, await db.MovimientosFinancieros.CountAsync(m => cuentas.Contains(m.CuentaId) && m.Tipo == "LIQUIDACION", Ct));
        Assert.Equal(9, await db.MovimientosFinancieros.CountAsync(m => m.CompraId != null && compras.Contains(m.CompraId.Value) && m.Tipo == "REEMBOLSO", Ct));
        Assert.Equal(comisionInicial + 90m, await SistemaAsync(db, "PLATAFORMA"));
        Assert.Equal(impuestoInicial + 118.80m, await SistemaAsync(db, "IMPUESTOS"));
    }
    private static async Task ConciliarAsync(PromotoraDbContext db, decimal totalEsperado)
    {
        // Una única consulta compara ambas magnitudes en el mismo snapshot de PostgreSQL.
        var cuentas = await db.CuentasFinancieras.AsNoTracking().Select(c => new
        {
            c.Id,
            c.Saldo,
            Suma = db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m
        }).ToArrayAsync(Ct);
        Assert.All(cuentas, c => Assert.Equal(c.Suma, c.Saldo)); Assert.All(cuentas, c => Assert.True(c.Saldo >= 0m));
        Assert.Equal(totalEsperado, cuentas.Sum(c => c.Saldo));
    }
    private static Task<decimal> SistemaAsync(PromotoraDbContext db, string tipo) => db.CuentasFinancieras.Where(c => c.Tipo == tipo && c.Moneda == "GTQ").Select(c => c.Saldo).SingleAsync(Ct);
    private ServiceProvider Servicios(int admin)
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(postgres.ConnectionString); services.AddCuentasFinancieras(); services.AddReembolsos();
        services.AddScoped<ISesionRepository, EfSesionRepository>(); services.AddSingleton<IUsuarioActual>(new Actual(admin));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
    private sealed record Actual(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
}
