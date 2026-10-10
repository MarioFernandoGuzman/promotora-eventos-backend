using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Finance;
using Promotora.Application.Ticketing;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Finance;

namespace Promotora.Infrastructure.Tests;

public sealed class FinanzasRegistrationTests
{
    [Fact]
    public void CompositionRootEntregaTasasConfiguradasALaFactoryDeApplication()
    {
        var services = new ServiceCollection();
        services.AddFinanzas(0.15m, 0.20m);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var politica = scope.ServiceProvider.GetRequiredService<IPoliticaTarifariaProvider>().Obtener();
        Assert.Equal(0.15m, politica.TasaIva); Assert.Equal(0.20m, politica.ComisionPorcentaje);
        var ahora = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var compra = scope.ServiceProvider.GetRequiredService<CrearCompraFactory>().Crear(Guid.NewGuid(), 1, 2, ahora, ahora.AddMinutes(5), "clave", "GTQ",
            [new EntradaCompra(3, 2, 1, 300m)], 5).Value;
        Assert.Equal(60m, compra.Importes.ComisionPlataforma);
        Assert.Equal(54m, compra.Importes.Impuestos); Assert.Equal(414m, compra.Importes.Total);
    }

    [Fact]
    public void ConfiguracionNoAdmiteTasasNegativas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddFinanzas(-0.01m, 0.10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddFinanzas(0.12m, -0.01m));
    }
}
