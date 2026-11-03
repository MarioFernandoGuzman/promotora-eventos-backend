using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Notifications;
using Promotora.Infrastructure.Shared.Messaging;

namespace Promotora.Infrastructure.Shared.Notifications;

public sealed class EnviarConfirmacionConsumer(RabbitMqOptions rabbit, IServiceScopeFactory scopes) : INotificacionConsumer
{
    public Task EjecutarAsync(CancellationToken ct) => NotificacionRabbitConsumer.EjecutarAsync(rabbit, scopes,
        "notificaciones.compra", TiposMensaje.CompraCompletada, static (services, id, payload, token) =>
        {
            Guid? publicId = null;
            if (payload is JsonElement json && json.TryGetProperty("publicId", out var value)
                && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var guid) && guid != Guid.Empty) publicId = guid;
            return services.GetRequiredService<INotificacionMensajePort>().ProcesarAsync(new(id, publicId, publicId.HasValue), token);
        }, ct);
}
