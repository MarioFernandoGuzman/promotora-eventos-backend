using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Images;

public sealed class SubirImagenHandler(IImagenStorage storage, IImagenResourceRepository resources, ImagenAccess access,
    ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<SubirImagenCommand, ImagenSubidaDto>
{
    public async Task<Result<ImagenSubidaDto>> HandleAsync(SubirImagenCommand request, CancellationToken ct)
    {
        if (request.Id <= 0 || request.Longitud <= 0 || request.Longitud > ImagenLimites.MaxArchivoBytes
            || request.ContentType is not ("image/png" or "image/jpeg")) return Result<ImagenSubidaDto>.Failure(ImagenLimites.Invalida);
        ImagenGuardada? guardada = null;
        bool committed = false;
        try
        {
            var result = await transaction.ExecuteAsync(async token =>
            {
                var permission = await access.AuthorizeAsync(request.Destino, request.Id, token);
                if (permission.IsFailure) return Result<ImagenSubidaDto>.Failure(permission.Errors);
                // Reutiliza el archivo en un reintento completo de BD, sin consumir de nuevo el stream.
                if (guardada is null)
                {
                    var saved = await storage.SaveAsync(request.Contenido, request.ContentType, token);
                    if (saved.IsFailure) return Result<ImagenSubidaDto>.Failure(saved.Errors);
                    guardada = saved.Value;
                }
                if (!await resources.AssignAsync(request.Destino, request.Id, guardada.Url, permission.Value, token))
                    return Result<ImagenSubidaDto>.Failure(new Error("images.concurrent", "El recurso cambió; vuelve a intentarlo.", ErrorType.Conflict));
                string entidad = request.Destino switch { DestinoImagen.Recinto => "recintos", DestinoImagen.Evento => "eventos", _ => "presentadores" };
                await audit.WriteAsync(actual.UsuarioId, "UPDATE", entidad, request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
                return Result<ImagenSubidaDto>.Success(new(guardada.Url));
            }, ct);
            committed = result.IsSuccess;
            return result;
        }
        finally
        {
            if (!committed && guardada is not null) await storage.DeleteUncommittedAsync(guardada.Nombre, CancellationToken.None);
        }
    }
}

public sealed class ObtenerImagenHandler(IImagenStorage storage, IImagenResourceRepository resources) : IQueryHandler<ObtenerImagenQuery, ImagenContenidoDto>
{
    public async Task<Result<ImagenContenidoDto>> HandleAsync(ObtenerImagenQuery request, CancellationToken ct)
    {
        if (!ImagenLimites.NombreValido(request.Nombre) || !await resources.PublicReferenceAsync(ImagenLimites.Url(request.Nombre), ct))
            return Result<ImagenContenidoDto>.Failure(ImagenLimites.NoEncontrada);
        var bytes = await storage.ReadAsync(request.Nombre, ct);
        return bytes is null ? Result<ImagenContenidoDto>.Failure(ImagenLimites.NoEncontrada) : Result<ImagenContenidoDto>.Success(new(bytes));
    }
}
