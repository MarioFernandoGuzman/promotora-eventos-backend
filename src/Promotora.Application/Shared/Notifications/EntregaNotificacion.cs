using System.Globalization;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Notifications;

// Servicio compartido, no invoca handlers de otros módulos.
internal static class EntregaNotificacion
{
    public static async Task ProcesarAsync(long mensajeId, string consumidor, string tipo, Func<CancellationToken, Task<ContenidoNotificacion?>> contenido,
        INotificacionRepository repository, IMensajeProcesadoRepository mensajes, IUnitOfWork unitOfWork,
        IEmailSender correo, NotificacionOptions options, CancellationToken ct)
    {
        options.Validar();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await mensajes.RegistrarAsync(mensajeId, consumidor, token)) return Result<Unit>.Success(Unit.Value);
            var destino = await contenido(token);
            if (destino is null)
            {
                await repository.RegistrarAsync(null, tipo, "FALLIDO", "Mensaje inválido o entidad no habilitada para notificación.", token);
                return Result<Unit>.Success(Unit.Value);
            }
            bool enviado = false;
            for (int intento = 1; intento <= options.MaxIntentos; intento++)
            {
                try
                {
                    await correo.EnviarAsync(new(destino.Email, destino.Asunto, destino.Texto,
                        $"{tipo}-{mensajeId.ToString(CultureInfo.InvariantCulture)}@promotora.invalid"), token);
                    enviado = true; break;
                }
                catch (CorreoEntregaException)
                {
                    if (intento < options.MaxIntentos) await Task.Delay(options.RetryDelayMilliseconds, token);
                }
            }
            await repository.RegistrarAsync(destino.UsuarioId, tipo, enviado ? "ENVIADO" : "FALLIDO",
                enviado ? "Correo entregado al servidor SMTP." : "SMTP rechazó la entrega tras los intentos configurados.", token);
            return Result<Unit>.Success(Unit.Value);
        }, ct);
    }
}
