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
        services.AddScoped<ILocalidadRepository, EfLocalidadRepository>();
        services.AddScoped<IVenuesTransaction, EfVenuesTransaction>();
        services.AddScoped<VenuesAccess>();
        services.AddScoped<LocalidadAccess>();
        services.AddScoped<IValidator<CrearLocalidadCommand>, CrearLocalidadValidator>();
        services.AddScoped<IValidator<ActualizarLocalidadCommand>, ActualizarLocalidadValidator>();
        services.AddScoped<IValidator<ListarLocalidadesQuery>, ListarLocalidadesValidator>();
        services.AddCommandHandler<CrearLocalidadCommand, LocalidadDto, CrearLocalidadHandler>();
        services.AddCommandHandler<ActualizarLocalidadCommand, LocalidadDto, ActualizarLocalidadHandler>();
        services.AddCommandHandler<BajaLocalidadCommand, Unit, BajaLocalidadHandler>();
        services.AddQueryHandler<ObtenerLocalidadQuery, LocalidadDto, ObtenerLocalidadHandler>();
        services.AddQueryHandler<ListarLocalidadesQuery, LocalidadesPaginaDto, ListarLocalidadesHandler>();
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
