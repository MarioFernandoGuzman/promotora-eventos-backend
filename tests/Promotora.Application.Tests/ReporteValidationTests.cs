using Promotora.Application.Reporting;

namespace Promotora.Application.Tests;

public sealed class ReporteValidationTests
{
    [Fact]
    public async Task AceptaFiltrosAcotadosYRechazaPaginasFechasYCodigosInvalidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var validator = new ReporteValidator();
        Assert.True((await validator.ValidateAsync(new ReporteQuery("R-01", new(Moneda: "GTQ")), ct)).IsValid);
        var invalid = new ReporteQuery("R-99", new(new(2026, 2, 2), new(2026, 2, 1), EventoId: -1, Moneda: "gtq", Page: 0, PageSize: 101));
        Assert.False((await validator.ValidateAsync(invalid, ct)).IsValid);
    }
}
