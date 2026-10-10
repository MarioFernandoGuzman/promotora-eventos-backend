using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Notifications;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Shared.Messaging;

namespace Promotora.Infrastructure.Shared.Notifications;

public static class NotificacionRegistration
{
    public static IServiceCollection AddBienvenidaNotificaciones(this IServiceCollection services)
    {
        services.AddScoped<IBienvenidaMensajePort, EnviarBienvenidaHandler>();
        services.AddSingleton<INotificacionConsumer, EnviarBienvenidaConsumer>();
        return services;
    }

    public static IServiceCollection AddConfirmacionNotificaciones(this IServiceCollection services, SmtpOptions smtp, NotificacionOptions options)
    {
        smtp.Validar(); options.Validar();
        services.AddSingleton(smtp); services.AddSingleton(options);
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IMensajeProcesadoRepository, EfMensajeProcesadoRepository>();
        services.AddScoped<INotificacionRepository, EfNotificacionRepository>();
        services.AddScoped<INotificacionMensajePort, EnviarConfirmacionHandler>();
        services.AddSingleton<INotificacionConsumer, EnviarConfirmacionConsumer>();
        return services;
    }
}
