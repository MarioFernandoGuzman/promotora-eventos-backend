using System.Globalization;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Notifications;

public sealed class EnviarConfirmacionHandler(INotificacionRepository repository, IMensajeProcesadoRepository mensajes,
    IUnitOfWork unitOfWork, IEmailSender correo, NotificacionOptions options) : INotificacionMensajePort
{
    public async Task ProcesarAsync(MensajeNotificacion mensaje, CancellationToken ct)
    {
        options.Validar();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await mensajes.RegistrarAsync(mensaje.MensajeId, "EnviarConfirmacionConsumer", token))
                return Result<Unit>.Success(Unit.Value);
            var contenido = mensaje.Valido && mensaje.CompraPublicId is Guid id && id != Guid.Empty
                ? await repository.CompraAsync(id, token) : null;
            if (contenido is null)
            {
                await repository.RegistrarAsync(null, "compra.completada", "FALLIDO", "Mensaje inválido o compra sin respaldo y emisión completos.", token);
                return Result<Unit>.Success(Unit.Value);
            }
            bool enviado = false;
            for (int intento = 1; intento <= options.MaxIntentos; intento++)
            {
                try
                {
                    await correo.EnviarAsync(new(contenido.Email, contenido.Asunto, contenido.Texto,
                        $"compra-{mensaje.MensajeId.ToString(CultureInfo.InvariantCulture)}@promotora.invalid"), token);
                    enviado = true; break;
                }
                catch (CorreoEntregaException)
                {
                    if (intento < options.MaxIntentos) await Task.Delay(options.RetryDelayMilliseconds, token);
                }
            }
            await repository.RegistrarAsync(contenido.UsuarioId, "compra.completada", enviado ? "ENVIADO" : "FALLIDO",
                enviado ? "Confirmación entregada al servidor SMTP." : "SMTP rechazó la entrega tras los intentos configurados.", token);
            return Result<Unit>.Success(Unit.Value);
        }, ct);
    }
}
