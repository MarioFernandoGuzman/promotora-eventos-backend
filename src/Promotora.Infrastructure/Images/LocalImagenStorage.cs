using Promotora.Application.Shared.Images;
using Promotora.Domain.Shared;
using SkiaSharp;

namespace Promotora.Infrastructure.Images;

public sealed class LocalImagenStorage(string rootPath) : IImagenStorage
{
    private readonly string root = Path.GetFullPath(rootPath);
    public async Task<Result<ImagenGuardada>> SaveAsync(Stream contenido, string contentType, CancellationToken ct)
    {
        if (contentType is not ("image/png" or "image/jpeg")) return Result<ImagenGuardada>.Failure(ImagenLimites.Invalida);
        using var input = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await contenido.ReadAsync(buffer, ct)) > 0)
        {
            if (input.Length + read > ImagenLimites.MaxArchivoBytes) return Result<ImagenGuardada>.Failure(ImagenLimites.Invalida);
            await input.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (input.Length == 0) return Result<ImagenGuardada>.Failure(ImagenLimites.Invalida);
        using var data = SKData.CreateCopy(input.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null || (contentType == "image/png" && codec.EncodedFormat != SKEncodedImageFormat.Png)
            || (contentType == "image/jpeg" && codec.EncodedFormat != SKEncodedImageFormat.Jpeg)
            || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > ImagenLimites.MaxDimension || codec.Info.Height > ImagenLimites.MaxDimension
            || (long)codec.Info.Width * codec.Info.Height > ImagenLimites.MaxPixeles || codec.FrameCount > 1)
            return Result<ImagenGuardada>.Failure(ImagenLimites.Invalida);
        ct.ThrowIfCancellationRequested();
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success) return Result<ImagenGuardada>.Failure(ImagenLimites.Invalida);
        using var image = SKImage.FromBitmap(bitmap);
        using var normalized = image.Encode(SKEncodedImageFormat.Png, 100);
        if (normalized is null || normalized.Size > ImagenLimites.MaxSalidaBytes) return Result<ImagenGuardada>.Failure(ImagenLimites.Invalida);
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        string nombre = $"{Guid.NewGuid():N}.png";
        string path = SafePath(nombre);
        bool created = false;
        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                created = true;
                await file.WriteAsync(normalized.ToArray(), ct);
            }
            return Result<ImagenGuardada>.Success(new(nombre, ImagenLimites.Url(nombre)));
        }
        catch
        {
            if (created) File.Delete(path); // Solo el archivo nuevo generado en este intento.
            throw;
        }
    }

    public async Task<byte[]?> ReadAsync(string nombre, CancellationToken ct)
    {
        if (!ImagenLimites.NombreValido(nombre)) return null;
        try
        {
            await using var file = new FileStream(SafePath(nombre), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            if (file.Length > ImagenLimites.MaxSalidaBytes) return null;
            using var bytes = new MemoryStream();
            await file.CopyToAsync(bytes, ct);
            return bytes.ToArray();
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    public Task DeleteUncommittedAsync(string nombre, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(SafePath(nombre));
        return Task.CompletedTask;
    }
    private string SafePath(string nombre)
    {
        if (!ImagenLimites.NombreValido(nombre)) throw new ArgumentException("Nombre de imagen inválido.");
        return Path.Combine(root, nombre);
    }
}
