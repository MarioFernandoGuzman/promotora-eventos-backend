using Promotora.Application.Shared.Ports;

namespace Promotora.Application.Shared.Notifications;

public sealed class EnviarConfirmacionHandler(INotificacionRepository repository, IMensajeProcesadoRepository mensajes,
    IUnitOfWork unitOfWork, IEmailSender correo, NotificacionOptions options) : INotificacionMensajePort
{
    public Task ProcesarAsync(MensajeNotificacion mensaje, CancellationToken ct) => EntregaNotificacion.ProcesarAsync(
        mensaje.MensajeId, "EnviarConfirmacionConsumer", "compra.completada",
        token => mensaje.Valido && mensaje.CompraPublicId is Guid id && id != Guid.Empty
            ? repository.CompraAsync(id, token) : Task.FromResult<ContenidoNotificacion?>(null),
        repository, mensajes, unitOfWork, correo, options, ct);
}
