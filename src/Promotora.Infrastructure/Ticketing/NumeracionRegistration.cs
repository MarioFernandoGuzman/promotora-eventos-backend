using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared;
using Promotora.Application.Ticketing.Numeracion;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Ticketing;

public static class NumeracionRegistration
{
    public static IServiceCollection AddNumeracion(this IServiceCollection services)
    {
        services.AddScoped<IDistribucionRepository, EfDistribucionRepository>();
        services.AddScoped<DistribucionAccess>();
        services.AddScoped<IValidator<GuardarDistribucionCommand>, GuardarDistribucionValidator>();
        services.AddScoped<IValidator<PublicarDistribucionCommand>, PublicarDistribucionValidator>();
        services.AddScoped<IValidator<BloquearAsientosCommand>, BloquearAsientosValidator>();
        services.AddScoped<IValidator<ObtenerDistribucionQuery>, ObtenerDistribucionValidator>();
        services.AddScoped<IValidator<ListarAsientosQuery>, ListarAsientosValidator>();
        services.AddCommandHandler<GuardarDistribucionCommand, DistribucionDto, GuardarDistribucionHandler>();
        services.AddCommandHandler<PublicarDistribucionCommand, DistribucionDto, PublicarDistribucionHandler>();
        services.AddCommandHandler<BloquearAsientosCommand, Unit, BloquearAsientosHandler>();
        services.AddQueryHandler<ObtenerDistribucionQuery, DistribucionDto, ObtenerDistribucionHandler>();
        services.AddQueryHandler<ListarAsientosQuery, DistribucionDto, ListarAsientosHandler>();
        return services;
    }
}
