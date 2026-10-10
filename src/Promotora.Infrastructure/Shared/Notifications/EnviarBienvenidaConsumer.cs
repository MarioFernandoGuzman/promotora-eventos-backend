using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Notifications;
using Promotora.Infrastructure.Shared.Messaging;

namespace Promotora.Infrastructure.Shared.Notifications;

public sealed class EnviarBienvenidaConsumer(RabbitMqOptions rabbit, IServiceScopeFactory scopes) : INotificacionConsumer
{
    public Task EjecutarAsync(CancellationToken ct) => NotificacionRabbitConsumer.EjecutarAsync(rabbit, scopes,
        "notificaciones.registro", TiposMensaje.UsuarioRegistrado, static (services, id, payload, token) =>
        {
            int? usuarioId = null;
            if (payload is JsonElement json && json.TryGetProperty("usuarioId", out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var usuario) && usuario > 0) usuarioId = usuario;
            return services.GetRequiredService<IBienvenidaMensajePort>().ProcesarAsync(new(id, usuarioId, usuarioId.HasValue), token);
        }, ct);
}
