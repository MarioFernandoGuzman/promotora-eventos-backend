using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Venues;

public static class VenuesRegistration
{
    public static IServiceCollection AddVenues(this IServiceCollection services)
    {
        services.AddScoped<IRecintoRepository, EfRecintoRepository>();
        services.AddScoped<IVenuesTransaction, EfVenuesTransaction>();
        services.AddScoped<VenuesAccess>();
        services.AddScoped<IValidator<CrearRecintoCommand>, CrearRecintoValidator>();
        services.AddScoped<IValidator<ActualizarRecintoCommand>, ActualizarRecintoValidator>();
        services.AddScoped<IValidator<ListarRecintosQuery>, ListarRecintosValidator>();
        services.AddCommandHandler<CrearRecintoCommand, RecintoDto, CrearRecintoHandler>();
        services.AddCommandHandler<ActualizarRecintoCommand, RecintoDto, ActualizarRecintoHandler>();
        services.AddCommandHandler<BajaRecintoCommand, Unit, BajaRecintoHandler>();
        services.AddQueryHandler<ObtenerRecintoQuery, RecintoDto, ObtenerRecintoHandler>();
        services.AddQueryHandler<ListarRecintosQuery, RecintosPaginaDto, ListarRecintosHandler>();
        return services;
    }
}
