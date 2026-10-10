using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Images;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Images;

public static class ImagesRegistration
{
    public static IServiceCollection AddImages(this IServiceCollection services, string rootPath)
    {
        services.AddSingleton<IImagenStorage>(new LocalImagenStorage(rootPath));
        services.AddScoped<IImagenResourceRepository, EfImagenResourceRepository>();
        services.AddScoped<ImagenAccess>();
        services.AddCommandHandler<SubirImagenCommand, ImagenSubidaDto, SubirImagenHandler>();
        services.AddQueryHandler<ObtenerImagenQuery, ImagenContenidoDto, ObtenerImagenHandler>();
        return services;
    }
}
