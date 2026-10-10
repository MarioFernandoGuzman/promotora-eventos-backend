using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.Venues;

public sealed class EfVenuesTransaction(IUnitOfWork unitOfWork) : IVenuesTransaction
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(operation, ct);
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "40001" or "40P01" })
            {
                // Reintentar toda la transacción, nunca solo una sentencia de negocio.
                if (attempt == 2)
                {
                    return Result<T>.Failure(new Error("venues.concurrent", "El recurso cambió concurrentemente; vuelve a intentarlo.", ErrorType.Conflict));
                }
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "23514" })
            {
                return Result<T>.Failure(new Error("venues.integridad", "La operación incumple los límites de capacidad o la integridad del catálogo.", ErrorType.BusinessRule));
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "23505" })
            {
                return Result<T>.Failure(new Error("venues.duplicado", "Ya existe una localidad con ese nombre en el recinto.", ErrorType.Conflict));
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "23503" })
            {
                return Result<T>.Failure(new Error("venues.referencia", "No existe el recurso relacionado.", ErrorType.NotFound));
            }
        }

        throw new InvalidOperationException("Estado de reintento inesperado.");
    }

    private static PostgresException? Postgres(Exception exception) => exception as PostgresException
        ?? (exception as DbUpdateException)?.InnerException as PostgresException;
}
