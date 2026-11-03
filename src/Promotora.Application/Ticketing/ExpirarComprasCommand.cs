using System.Globalization;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

// Operación interna de mantenimiento; su ejecución periódica corresponde a F6-07.
public sealed record ExpirarComprasCommand(int Lote = 100) : ICommand<ExpiracionLoteDto>;
public sealed record ExpiracionLoteDto(int Revisadas, int Expiradas, bool OmitidoPorBloqueo = false);
public sealed class ExpirarComprasValidator : AbstractValidator<ExpirarComprasCommand>
{
    public ExpirarComprasValidator() => RuleFor(x => x.Lote).InclusiveBetween(1, 1000);
}

public sealed class ExpirarComprasHandler(IExpiracionCompraRepository compras, ICupoEntradaRepository cupos, ITicketingTransaction transaction,
    IClock clock, IAuditLogger audit) : ICommandHandler<ExpirarComprasCommand, ExpiracionLoteDto>
{
    public Task<Result<ExpiracionLoteDto>> HandleAsync(ExpirarComprasCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Lote is < 1 or > 1000) return Task.FromResult(Result<ExpiracionLoteDto>.Failure(new Error("compra.lote", "El lote debe estar entre 1 y 1000.", ErrorType.Validation)));
        return transaction.ExecuteAsync(async token =>
        {
            if (!await compras.IntentarCoordinarAsync(token))
                return Result<ExpiracionLoteDto>.Success(new(0, 0, true));
            var ahora = clock.UtcNow;
            var ids = await compras.VencidasAsync(ahora, request.Lote, token);
            int expiradas = 0;
            foreach (int id in ids)
            {
                // Revalidar estado/vencimiento en el UPDATE; otro proceso puede ganar desde la consulta.
                if (!await cupos.FinalizarYDevolverAsync(id, EstadoCompra.PENDIENTE, EstadoCompra.EXPIRADA, ahora, token)) continue;
                await audit.WriteAsync(null, "UPDATE", "compras", id.ToString(CultureInfo.InvariantCulture), null, token);
                expiradas++;
            }
            return Result<ExpiracionLoteDto>.Success(new(ids.Count, expiradas));
        }, ct);
    }
}
