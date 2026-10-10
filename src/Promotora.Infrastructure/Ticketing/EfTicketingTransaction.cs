using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfTicketingTransaction(IUnitOfWork unitOfWork) : ITicketingTransaction
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct)
    {
        for (int intento = 0; intento < 3; intento++)
        {
            try { return await unitOfWork.ExecuteInTransactionAsync(operation, ct); }
            catch (Exception ex) when (Postgres(ex) is { SqlState: "40001" or "40P01" }
                or { SqlState: "23505", ConstraintName: "uq_compras_idempotency_key" })
            {
                // Repetir toda la transacción: revalida actor y recupera la compra ganadora.
                if (intento == 2) return Result<T>.Failure(new Error("compra.concurrente", "Conflicto concurrente; reintenta con la misma clave.", ErrorType.Conflict));
            }
            catch (Exception ex) when (Postgres(ex) is { SqlState: "23514" or "23503" })
            {
                return Result<T>.Failure(new Error("compra.integridad", "La compra incumple la integridad del catálogo.", ErrorType.BusinessRule));
            }
        }
        throw new InvalidOperationException("Estado de reintento inesperado.");
    }

    private static PostgresException? Postgres(Exception ex) => ex as PostgresException ?? (ex as DbUpdateException)?.InnerException as PostgresException;
}
