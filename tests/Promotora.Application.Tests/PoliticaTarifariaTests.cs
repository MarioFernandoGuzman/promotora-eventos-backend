using Promotora.Application.Finance;
using Promotora.Application.Ticketing;
using Promotora.Domain.Finance;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Tests;

public sealed class PoliticaTarifariaTests
{
    [Fact]
    public void ApplicationInyectaPoliticaSinRecalcularComprasExistentes()
    {
        var provider = new PoliticaFake(new(0.12m, 0.10m));
        var factory = new CrearCompraFactory(provider);
        var ahora = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var primera = factory.Crear(Guid.NewGuid(), 1, 2, ahora, ahora.AddMinutes(5), "primera", "GTQ", [new EntradaCompra(3, 2, 2, 150m)], 5).Value;
        provider.Politica = new(0, 0);
        var nueva = factory.Crear(Guid.NewGuid(), 1, 2, ahora, ahora.AddMinutes(5), "nueva", "GTQ", [new EntradaCompra(3, 2, 2, 150m)], 5).Value;
        Assert.Equal(369.60m, primera.Importes.Total);
        Assert.Equal(300m, nueva.Importes.Total);
    }
    private sealed class PoliticaFake(PoliticaTarifaria politica) : IPoliticaTarifariaProvider
    {
        public PoliticaTarifaria Politica { get; set; } = politica;
        public PoliticaTarifaria Obtener() => Politica;
    }
}
