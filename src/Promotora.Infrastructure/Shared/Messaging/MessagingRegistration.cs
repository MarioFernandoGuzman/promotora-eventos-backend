using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Ticketing;

namespace Promotora.Infrastructure.Shared.Messaging;

public static class MessagingRegistration
{
    public static IServiceCollection AddCompraConsumerComponents(this IServiceCollection services)
    {
        services.AddScoped<IMensajeProcesadoRepository, EfMensajeProcesadoRepository>();
        services.AddScoped<ICompraTomaRepository, EfCompraTomaRepository>();
        return services;
    }

    public static IServiceCollection AddProcesamientoCompras(this IServiceCollection services)
    {
        services.AddCompraConsumerComponents();
        services.AddScoped<ICompraMensajeRepository, EfCompraMensajeRepository>();
        services.AddScoped<IProcesarCompraMensajePort, ProcesarCompraService>();
        return services;
    }

    public static IServiceCollection AddCompraConsumer(this IServiceCollection services, CompraConsumerOptions options)
    {
        if (!services.Any(s => s.ServiceType == typeof(IProcesarCompraMensajePort)))
            throw new InvalidOperationException("Consumidor deshabilitado: falta el integrador transaccional de pago, ledger y emisión de F7.");
        options.Validar();
        services.AddSingleton(options);
        services.AddSingleton<ICompraMessageConsumer, ProcesarCompraConsumer>();
        return services;
    }

    public static IServiceCollection AddOutboxRelay(this IServiceCollection services)
    {
        services.AddScoped<IOutboxRelay, EfOutboxRelay>();
        return services;
    }

    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, RabbitMqOptions options)
    {
        options.Validar();
        services.AddSingleton(options);
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        return services;
    }
}
