using System.Globalization;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

public sealed record RecuperarComprasCommand(int Lote = 100, int TimeoutMinutos = 5) : ICommand<RecuperacionLoteDto>;
public sealed record RecuperacionLoteDto(int Revisadas, int Completadas, int Fallidas, int Omitidas);
public sealed class RecuperarComprasValidator : AbstractValidator<RecuperarComprasCommand>
{
    public RecuperarComprasValidator()
    {
        RuleFor(x => x.Lote).InclusiveBetween(1, 1000);
        RuleFor(x => x.TimeoutMinutos).InclusiveBetween(1, 1440);
    }
}

public sealed class RecuperarComprasHandler(IRecuperacionCompraRepository compras, ICompraMensajeRepository mensajes,
    IMensajeProcesadoRepository dedup, IRegistroPagoPort pagos, IPaymentGateway gateway, ICompletarCompraPort completar,
    ICupoEntradaRepository cupos, IUnitOfWork transaction, IClock clock, IAuditLogger audit)
    : ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>
{
    public async Task<Result<RecuperacionLoteDto>> HandleAsync(RecuperarComprasCommand request, CancellationToken ct)
    {
        if (request.Lote is < 1 or > 1000 || request.TimeoutMinutos is < 1 or > 1440)
            return Result<RecuperacionLoteDto>.Failure(new Error("compra.recuperacion", "Lote o timeout inválidos.", ErrorType.Validation));
        var limite = clock.UtcNow.AddMinutes(-request.TimeoutMinutos);
        var candidatas = await compras.CandidatasAsync(limite, request.Lote, ct);
        int completadas = 0, fallidas = 0, omitidas = 0;
        foreach (var mensaje in candidatas)
        {
            var resultado = await transaction.ExecuteInTransactionAsync(async token =>
            {
                // Serializa recuperadores por mensaje. El consumidor normal solo toma PENDIENTE;
                // estas candidatas PROCESANDO no se toman de nuevo ni guardan marcadores sin efectos.
                if (!await dedup.RegistrarAsync(mensaje.MensajeId, "ProcesarCompraConsumer", token)) return Omitir();
                var compra = await compras.LockAsync(mensaje.PublicId, limite, token);
                if (compra is null) return Omitir(); // Failure provoca rollback del marcador provisional.
                var asistentes = await mensajes.LeerAsync(mensaje, token);
                if (asistentes.IsFailure) return Result<string>.Failure(asistentes.Errors);
                var respuesta = await gateway.ConsultarAsync(compra.PublicId, token);
                if (respuesta?.Estado != "APROBADO" && await compras.TieneEfectosConfirmadosAsync(compra.Id, token)) return Omitir();
                if (respuesta is not null)
                {
                    var intento = await pagos.IniciarEnTransaccionAsync(compra.PublicId, respuesta.Pasarela, token);
                    if (intento.IsFailure) return Result<string>.Failure(intento.Errors);
                    if (respuesta.Estado == "APROBADO")
                    {
                        var fin = await completar.EnTransaccionAsync(compra.PublicId, intento.Value.Id, respuesta, asistentes.Value, token);
                        return fin.IsSuccess ? Result<string>.Success("COMPLETADA") : Result<string>.Failure(fin.Errors);
                    }
                    if (respuesta.Estado != "RECHAZADO") return Omitir();
                    var rechazo = await pagos.ResolverEnTransaccionAsync(intento.Value.Id, respuesta, token);
                    if (rechazo.IsFailure) return Result<string>.Failure(rechazo.Errors);
                }
                if (!await cupos.FinalizarYDevolverAsync(compra.Id, EstadoCompra.PROCESANDO, EstadoCompra.FALLIDA, clock.UtcNow, token)) return Omitir();
                await audit.WriteAsync(null, "UPDATE", "compras", compra.Id.ToString(CultureInfo.InvariantCulture), null, token);
                return Result<string>.Success("FALLIDA");
            }, ct);
            if (resultado.IsFailure) omitidas++;
            else if (resultado.Value == "COMPLETADA") completadas++;
            else fallidas++;
        }
        return Result<RecuperacionLoteDto>.Success(new(candidatas.Count, completadas, fallidas, omitidas));
    }

    private static Result<string> Omitir() => Result<string>.Failure(new Error("compra.recuperacion_omitida", "La compra no admite recuperación automática.", ErrorType.Conflict));
}
