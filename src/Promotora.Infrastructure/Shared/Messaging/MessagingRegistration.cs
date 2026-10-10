using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Ports;

namespace Promotora.Infrastructure.Shared.Messaging;

public static class MessagingRegistration
{
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
