using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Finance.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Tests;

public sealed class FakePaymentGatewayTests
{
    [Theory]
    [InlineData(0, "APROBADO")]
    [InlineData(1, "RECHAZADO")]
    public async Task TasaConfigurableYRepeticionConcurrenteConservanResultado(int tasa, string estado)
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = Gateway(tasa);
        var solicitud = new SolicitudPago(Guid.NewGuid(), 369.60m, "GTQ");
        Assert.Null(await gateway.ConsultarAsync(solicitud.Referencia, ct));
        var pagos = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => gateway.CobrarAsync(solicitud, ct)));
        Assert.All(pagos, p => { Assert.True(p.IsSuccess); Assert.Equal(estado, p.Value.Estado); Assert.Same(pagos[0].Value, p.Value); });
        Assert.Same(pagos[0].Value, await gateway.ConsultarAsync(solicitud.Referencia, ct));
        Assert.Equal(DateTimeKind.Utc, pagos[0].Value.Fecha.Kind);
        var diferente = await gateway.CobrarAsync(solicitud with { Monto = 400m }, ct);
        Assert.Equal(ErrorType.Conflict, Assert.Single(diferente.Errors).Type);
        Assert.Equal(369.60m, (await gateway.ConsultarAsync(solicitud.Referencia, ct))!.Monto);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(100000000)]
    public async Task RechazaMontosInvalidosSinCrearPago(decimal monto)
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = Gateway(0m); var referencia = Guid.NewGuid();
        Assert.True((await gateway.CobrarAsync(new(referencia, monto, "GTQ"), ct)).IsFailure);
        Assert.Null(await gateway.ConsultarAsync(referencia, ct));
    }

    [Fact]
    public async Task GratuitaNoFallaYCancelacionNoCreaPago()
    {
        var gateway = Gateway(1m); var ct = TestContext.Current.CancellationToken;
        Assert.Equal("APROBADO", (await gateway.CobrarAsync(new(Guid.NewGuid(), 0m, "GTQ"), ct)).Value.Estado);
        var referencia = Guid.NewGuid();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.CobrarAsync(new(referencia, 1m, "GTQ"), cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.ConsultarAsync(referencia, cancelled.Token));
        Assert.Null(await gateway.ConsultarAsync(referencia, ct));
        Assert.True((await gateway.CobrarAsync(new(Guid.Empty, 1m, "GTQ"), ct)).IsFailure);
        Assert.True((await gateway.CobrarAsync(new(Guid.NewGuid(), 1m, "USD"), ct)).IsFailure);
    }

    [Fact]
    public void RegistroEsSingletonYRechazaTasasFueraDeRango()
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(null); services.AddPagoSimulado(new(0.5m));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Same(provider.GetRequiredService<IPaymentGateway>(), provider.GetRequiredService<IPaymentGateway>());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddPagoSimulado(new(-0.01m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddPagoSimulado(new(1.01m)));
    }

    [Fact]
    public async Task MismaConfiguracionProduceResultadoEstableEnOtraInstancia()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = Gateway(0.5m); var second = Gateway(0.5m);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 100).Select(async _ =>
        {
            var solicitud = new SolicitudPago(Guid.NewGuid(), 100m, "GTQ");
            var a = (await first.CobrarAsync(solicitud, ct)).Value; var b = (await second.CobrarAsync(solicitud, ct)).Value;
            Assert.Equal(a.Estado, b.Estado);
            return a.Estado;
        }));
        Assert.Contains("APROBADO", resultados); Assert.Contains("RECHAZADO", resultados);
    }

    private static FakePaymentGateway Gateway(decimal tasa) => new(new(tasa), new SystemClock(TimeProvider.System));
}
