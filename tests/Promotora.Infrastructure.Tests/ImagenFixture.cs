using SkiaSharp;
namespace Promotora.Infrastructure.Tests;

public static class ImagenFixture
{
    public static byte[] Imagen(SKEncodedImageFormat formato = SKEncodedImageFormat.Png, int width = 2, int height = 3)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(formato, 90);
        return data.ToArray();
    }

}
