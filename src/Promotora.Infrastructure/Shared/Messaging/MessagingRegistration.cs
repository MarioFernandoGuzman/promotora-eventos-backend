using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Ports;

namespace Promotora.Infrastructure.Shared.Messaging;

public static class MessagingRegistration
{
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, RabbitMqOptions options)
    {
        options.Validar();
        services.AddSingleton(options);
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        return services;
    }
}
