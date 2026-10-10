namespace Promotora.Application.Shared.Notifications;

public sealed record MensajeNotificacion(long MensajeId, Guid? CompraPublicId, bool Valido);
public sealed record MensajeBienvenida(long MensajeId, int? UsuarioId, bool Valido);
public sealed record ContenidoNotificacion(int UsuarioId, string Email, string Asunto, string Texto);
public sealed record NotificacionOptions(int MaxIntentos = 3, int RetryDelayMilliseconds = 2000)
{
    public void Validar()
    {
        if (MaxIntentos is < 1 or > 10 || RetryDelayMilliseconds is < 1 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(MaxIntentos), "Opciones de notificación fuera de rango.");
    }
}

public interface INotificacionRepository
{
    Task<ContenidoNotificacion?> CompraAsync(Guid publicId, CancellationToken ct);
    Task<ContenidoNotificacion?> UsuarioAsync(int usuarioId, CancellationToken ct);
    Task RegistrarAsync(int? usuarioId, string tipo, string estado, string detalle, CancellationToken ct);
}

public interface INotificacionMensajePort
{
    Task ProcesarAsync(MensajeNotificacion mensaje, CancellationToken ct);
}

public interface INotificacionConsumer
{
    Task EjecutarAsync(CancellationToken ct);
}

public interface IBienvenidaMensajePort
{
    Task ProcesarAsync(MensajeBienvenida mensaje, CancellationToken ct);
}
