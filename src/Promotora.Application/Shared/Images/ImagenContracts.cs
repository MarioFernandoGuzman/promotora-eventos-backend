using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Images;

public enum DestinoImagen { Recinto, Evento, Presentador }
public sealed record SubirImagenCommand(DestinoImagen Destino, int Id, Stream Contenido, long Longitud, string ContentType) : ICommand<ImagenSubidaDto>;
public sealed record ImagenSubidaDto(string Url);
public sealed record ImagenGuardada(string Nombre, string Url);
public sealed record ImagenContenidoDto(byte[] Contenido);
public sealed record ObtenerImagenQuery(string Nombre) : IQuery<ImagenContenidoDto>;

public static class ImagenLimites
{
    public const int MaxArchivoBytes = 5 * 1024 * 1024;
    public const int MaxSalidaBytes = 10 * 1024 * 1024;
    public const int MaxDimension = 4096;
    public const long MaxPixeles = 16000000;
    public static readonly Error Invalida = new("images.invalid", "Se requiere PNG/JPEG válido de hasta 5 MiB, 4096 por lado y 16 millones de píxeles.", ErrorType.Validation);
    public static readonly Error NoEncontrada = new("images.not_found", "No se encontró una imagen pública.", ErrorType.NotFound);
    public static bool NombreValido(string nombre) => nombre.Length == 36 && nombre.EndsWith(".png", StringComparison.Ordinal)
        && nombre.AsSpan(0, 32).IndexOfAnyExcept("0123456789abcdef") < 0;
    public static string Url(string nombre) => $"/api/v1/imagenes/{nombre}";
}

public interface IImagenStorage
{
    Task<Result<ImagenGuardada>> SaveAsync(Stream contenido, string contentType, CancellationToken ct);
    Task<byte[]?> ReadAsync(string nombre, CancellationToken ct);
    Task DeleteUncommittedAsync(string nombre, CancellationToken ct);
}
public interface IImagenResourceRepository
{
    Task<bool> AssignAsync(DestinoImagen destino, int id, string url, string? estadoEsperado, CancellationToken ct);
    Task<bool> PublicReferenceAsync(string url, CancellationToken ct);
}
