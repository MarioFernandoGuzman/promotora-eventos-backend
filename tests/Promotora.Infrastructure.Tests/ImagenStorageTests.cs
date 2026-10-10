using System.Text;
using Promotora.Application.Shared.Images;
using Promotora.Infrastructure.Images;
using SkiaSharp;
using static Promotora.Infrastructure.Tests.ImagenFixture;

namespace Promotora.Infrastructure.Tests;

public sealed class ImagenStorageTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "promotora-images-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    public async Task NormalizaPixelesANuevoPngYEliminaContenidoAdicional(SKEncodedImageFormat formato, string mime)
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = new LocalImagenStorage(root);
        byte[] entrada = [.. Imagen(formato), .. Encoding.UTF8.GetBytes("<script>contenido-adicional</script>")];
        using var stream = new MemoryStream(entrada);
        var result = await storage.SaveAsync(stream, mime, ct);
        Assert.True(result.IsSuccess);
        Assert.True(ImagenLimites.NombreValido(result.Value.Nombre));
        byte[] bytes = (await storage.ReadAsync(result.Value.Nombre, ct))!;
        using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        Assert.Equal(2, codec.Info.Width); Assert.Equal(3, codec.Info.Height);
        Assert.DoesNotContain("contenido-adicional", Encoding.UTF8.GetString(bytes));
        await storage.DeleteUncommittedAsync(result.Value.Nombre, ct);
        Assert.Null(await storage.ReadAsync(result.Value.Nombre, ct));
    }

    [Fact]
    public async Task RechazaSvgMimeInconsistenteArchivoTruncadoYSobredimensionado()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = new LocalImagenStorage(root);
        var cases = new (byte[] Bytes, string Mime)[] {
            (Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"), "image/png"),
            (Imagen(), "image/jpeg"), (Imagen()[..24], "image/png"),
            (Imagen(SKEncodedImageFormat.Png, 4097, 1), "image/png"),
            (Imagen(SKEncodedImageFormat.Png, 4001, 4001), "image/png"),
            (new byte[ImagenLimites.MaxArchivoBytes + 1], "image/png") };
        foreach (var item in cases)
        {
            using var stream = new MemoryStream(item.Bytes);
            Assert.True((await storage.SaveAsync(stream, item.Mime, ct)).IsFailure);
        }
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task NuncaResuelveNombresFueraDeSuDirectorio()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = new LocalImagenStorage(root);
        foreach (string name in new[] { "../appsettings.json", "C:\\secret.png", "../../" + new string('a', 32) + ".png", new string('a', 32) + ".svg" })
        {
            Assert.Null(await storage.ReadAsync(name, ct));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteUncommittedAsync(name, ct));
        }
    }
}
