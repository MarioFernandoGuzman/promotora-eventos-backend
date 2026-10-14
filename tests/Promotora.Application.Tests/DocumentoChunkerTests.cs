using System.Text;
using Promotora.Application.AI.Documentos;

namespace Promotora.Application.Tests;

public sealed class DocumentoChunkerTests
{
    [Fact]
    public void ConservaPaginaFisicaOrdenYSolapamiento()
    {
        var chunks = new DocumentoChunker().Dividir(new string('a', 6000) + "\f\fTercera página", "PDF", TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 1, 2, 3 }, chunks.Select(c => c.Orden)); Assert.Equal(new int?[] { 1, 1, 3 }, chunks.Select(c => c.Pagina));
        Assert.Equal(3200, chunks[0].Contenido.Length); Assert.Equal(3200, chunks[1].Contenido.Length);
        Assert.Equal(chunks[0].Contenido[^400..], chunks[1].Contenido[..400]);
    }
    [Fact]
    public void UnicodeNoRompeEmojisYRespetaLimiteDelProveedor()
    {
        string texto = string.Concat(Enumerable.Repeat("👩🏽‍💻", 1800));
        var chunks = new DocumentoChunker().Dividir(texto, "MD", TestContext.Current.CancellationToken);
        Assert.All(chunks, c => { Assert.Null(c.Pagina); Assert.InRange(Encoding.UTF8.GetByteCount(c.Contenido), 1, 5500); Assert.DoesNotContain("�", c.Contenido); });
        Assert.True(chunks.Count > 1);
    }
    [Fact]
    public void CancelacionSePropaga()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => new DocumentoChunker().Dividir("texto", "TXT", cts.Token));
    }
}
