using Microsoft.EntityFrameworkCore;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Infrastructure.Shared.Persistence;

public sealed class EfUnitOfWork(PromotoraDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("La unidad de trabajo no admite transacciones anidadas.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation(ct);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            // DisposeAsync revierte incluso si el token fue cancelado.
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
