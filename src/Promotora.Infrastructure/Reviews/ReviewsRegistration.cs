using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Reviews;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Reviews;

public static class ReviewsRegistration
{
    public static IServiceCollection AddReviews(this IServiceCollection services)
    {
        services.AddScoped<IResenaRepository, EfResenaRepository>();
        services.AddScoped<ResenaAccess>();
        services.AddScoped<IValidator<ResenasEventoQuery>, ResenasEventoValidator>();
        services.AddQueryHandler<ResenasEventoQuery, Promotora.Application.Shared.PaginaDto<ResenaDto>, ResenasEventoHandler>();
        services.AddScoped<IValidator<ModerarResenaCommand>, ModerarResenaValidator>();
        services.AddScoped<IValidator<ResenasAdministracionQuery>, ResenasAdministracionValidator>();
        services.AddCommandHandler<ModerarResenaCommand, ResenaDto, ModerarResenaHandler>();
        services.AddQueryHandler<ResenasAdministracionQuery, Promotora.Application.Shared.PaginaDto<ResenaDto>, ResenasAdministracionHandler>();
        services.AddScoped<IValidator<CrearResenaCommand>, CrearResenaValidator>();
        services.AddCommandHandler<CrearResenaCommand, ResenaDto, CrearResenaHandler>();
        return services;
    }
}
