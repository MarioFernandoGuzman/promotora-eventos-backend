using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Application.Venues;
using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing;

public sealed class ValidarBoletoHandler(IValidacionBoletoRepository boletos, ITicketTokenService tokens, VenuesAccess access,
    IUsuarioActual actual, ITicketingTransaction transaction, IClock clock, IAuditLogger audit) : ICommandHandler<ValidarBoletoCommand, BoletoValidadoDto>
{
    public Task<Result<BoletoValidadoDto>> HandleAsync(ValidarBoletoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var actor = await access.ActorAsync(true, token);
        if (actor is null) return Result<BoletoValidadoDto>.Failure(VenuesAccess.Forbidden);
        if (!tokens.Validar(request.CodigoQr, out var publicId))
            return Result<BoletoValidadoDto>.Failure(new Error("boleto.firma", "Firma de boleto inválida.", ErrorType.BusinessRule));
        var boleto = await boletos.LockAsync(publicId, request.CodigoQr, token);
        if (boleto is null || !VenuesAccess.Permite(actor, boleto.DuenoId))
            return Result<BoletoValidadoDto>.Failure(new Error("boleto.no_encontrado", "Boleto no encontrado.", ErrorType.NotFound));
        if (boleto.EstadoCompra != "COMPLETADA" || boleto.Estado != "VALIDO") return Rechazado();
        var lectura = clock.UtcNow;
        // PostgreSQL conserva microsegundos: respuesta y fecha persistida deben coincidir.
        var ahora = new DateTime(lectura.Ticks - lectura.Ticks % 10, DateTimeKind.Utc);
        if (!await boletos.UsarAsync(boleto.Id, request.CodigoQr, actual.UsuarioId!.Value, ahora, token)) return Rechazado();
        await audit.WriteAsync(actual.UsuarioId, "VALIDATE", "boletos", boleto.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<BoletoValidadoDto>.Success(new(boleto.PublicId, "USADO", ahora));
    }, ct);

    private static Result<BoletoValidadoDto> Rechazado() => Result<BoletoValidadoDto>.Failure(new Error("boleto.estado", "El boleto no admite entrada.", ErrorType.Conflict));
}
