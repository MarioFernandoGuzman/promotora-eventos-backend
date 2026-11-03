using Promotora.Application.Shared.Ports;

namespace Promotora.Application.Shared.Notifications;

public sealed class EnviarBienvenidaHandler(INotificacionRepository repository, IMensajeProcesadoRepository mensajes,
    IUnitOfWork unitOfWork, IEmailSender correo, NotificacionOptions options) : IBienvenidaMensajePort
{
    public Task ProcesarAsync(MensajeBienvenida mensaje, CancellationToken ct) => EntregaNotificacion.ProcesarAsync(
        mensaje.MensajeId, "EnviarBienvenidaConsumer", "usuario.registrado",
        token => mensaje.Valido && mensaje.UsuarioId is > 0 ? repository.UsuarioAsync(mensaje.UsuarioId.Value, token)
            : Task.FromResult<ContenidoNotificacion?>(null), repository, mensajes, unitOfWork, correo, options, ct);
}
