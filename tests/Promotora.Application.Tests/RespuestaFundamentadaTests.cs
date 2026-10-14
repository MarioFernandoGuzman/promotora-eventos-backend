using Promotora.Application.AI.Rag;

namespace Promotora.Application.Tests;

public sealed class RespuestaFundamentadaTests
{
    [Fact]
    public void SoloAceptaCitaRecuperadaYExtractoLiteralSinTextoLibreInventado()
    {
        var servicio = new RespuestaFundamentada(); var contexto = servicio.Preparar([new("rag:5", "Reglamento", 2, 5, 1, null, "Las puertas abren a las 10:00 UTC.")]);
        var valido = servicio.Validar("""{"extractos":[{"fuente":"rag:5","texto":"Las puertas abren a las 10:00 UTC."}],"respuesta":"Invento un descuento"}""", contexto, true);
        Assert.True(valido.IsSuccess); Assert.Contains("Soy José".ToLowerInvariant(), valido.Value.Respuesta.ToLowerInvariant()); Assert.DoesNotContain("descuento", valido.Value.Respuesta);
        Assert.Equal(2, Assert.Single(valido.Value.Fuentes).Pagina);
        Assert.True(servicio.Validar("""{"extractos":[{"fuente":"rag:99","texto":"Las puertas abren a las 10:00 UTC."}]}""", contexto, false).IsFailure);
        Assert.True(servicio.Validar("""{"extractos":[{"fuente":"rag:5","texto":"Existe un descuento del 50 por ciento"}]}""", contexto, false).IsFailure);
        Assert.True(servicio.Validar("no es json", contexto, false).IsFailure);
    }
}
