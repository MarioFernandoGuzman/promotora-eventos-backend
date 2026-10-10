using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfCatalogTransaction(IUnitOfWork unitOfWork) : ICatalogTransaction
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try { return await unitOfWork.ExecuteInTransactionAsync(operation, ct); }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "40001" or "40P01" })
            {
                if (attempt == 2) return Result<T>.Failure(new Error("catalog.concurrent", "El catálogo cambió concurrentemente; vuelve a intentarlo.", ErrorType.Conflict));
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "23505" })
            {
                return Result<T>.Failure(new Error("catalog.duplicate", "El recurso o la asociación ya existe.", ErrorType.Conflict));
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "23514" })
            {
                return Result<T>.Failure(new Error("catalog.integridad", "La operación incumple las reglas de capacidad o integridad.", ErrorType.BusinessRule));
            }
            catch (Exception exception) when (Postgres(exception) is { SqlState: "23503" })
            {
                return Result<T>.Failure(CatalogAccess.NotFound);
            }
        }

        throw new InvalidOperationException("Estado de reintento inesperado.");
    }

    private static PostgresException? Postgres(Exception exception) => exception as PostgresException
        ?? (exception as DbUpdateException)?.InnerException as PostgresException;
}
