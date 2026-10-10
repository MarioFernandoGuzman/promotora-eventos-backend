using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Ticketing;

public static class TicketingRegistration
{
    public static IServiceCollection AddTicketing(this IServiceCollection services, CompraOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ReservaMinutos);
        if (options.Moneda != "GTQ") throw new ArgumentException("El catálogo actual utiliza GTQ.", nameof(options));
        services.AddSingleton(options);
        services.AddScoped<ICompraRepository, EfCompraRepository>();
        services.AddScoped<ICupoEntradaRepository, EfCupoEntradaRepository>();
        services.AddScoped<ITicketingTransaction, EfTicketingTransaction>();
        services.AddScoped<IValidator<CrearCompraCommand>, CrearCompraValidator>();
        services.AddCommandHandler<CrearCompraCommand, CrearCompraResultado, CrearCompraHandler>();
        return services;
    }
}
