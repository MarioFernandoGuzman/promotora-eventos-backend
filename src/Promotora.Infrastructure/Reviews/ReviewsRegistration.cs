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
        services.AddScoped<IValidator<CrearResenaCommand>, CrearResenaValidator>();
        services.AddCommandHandler<CrearResenaCommand, ResenaDto, CrearResenaHandler>();
        return services;
    }
}
