using Promotora.Application.AI.Documentos;

namespace Promotora.Application.Tests;

public sealed class DocumentoValidacionTests
{
    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 101, null)]
    [InlineData(100001, 20, null)]
    [InlineData(1, 20, "otro")]
    public async Task ListadosRechazanPaginacionYEstadoInvalidos(int page, int pageSize, string? estado)
    {
        Assert.False((await new DocumentosValidator().ValidateAsync(new DocumentosQuery(page, pageSize, estado), TestContext.Current.CancellationToken)).IsValid);
    }
    [Fact]
    public async Task CargaValidaYRechazaMetadatosQueNoCabenEnElEsquema()
    {
        using var stream = new MemoryStream([1]); var validator = new SubirDocumentoValidator();
        var correcto = new SubirDocumentoCommand("Reglamento", "reglamento.md", 1, stream);
        Assert.True((await validator.ValidateAsync(correcto, TestContext.Current.CancellationToken)).IsValid);
        foreach (var invalido in new[] { correcto with { Titulo = new string('a', 201) }, correcto with { Titulo = "Oculto\0" },
            correcto with { Longitud = 0 }, correcto with { DocumentoAnteriorId = 0 }, correcto with { NombreArchivo = new string('a', 256) } })
            Assert.False((await validator.ValidateAsync(invalido, TestContext.Current.CancellationToken)).IsValid);
    }
}
