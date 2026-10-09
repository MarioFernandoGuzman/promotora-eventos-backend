using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Identity.Persistence;

public sealed class EfSesionRepository(PromotoraDbContext db, IClock clock) : ISesionRepository
{
    public async Task<Result<T>> ExecuteAtomicAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("La operación de sesión no admite transacciones anidadas.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task LockUsuarioAsync(int usuarioId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El lock requiere una transacción.");
        }

        await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM public.usuarios WHERE id = {usuarioId} FOR UPDATE").ToListAsync(ct);
    }

    public async Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken ct) =>
        await db.RefreshTokens.AsNoTracking().Where(x => x.TokenHash == tokenHash)
            .Select(x => new RefreshToken(x.Id, x.UsuarioId, x.FamiliaId, x.ExpiraEn, x.RevocadoEn)).SingleOrDefaultAsync(ct);

    public async Task<long> AddAsync(int usuarioId, string hash, Guid familiaId, DateTime expiraEn, string? ip, CancellationToken ct)
    {
        var row = new RefreshTokenRow
        {
            UsuarioId = usuarioId,
            TokenHash = hash,
            FamiliaId = familiaId,
            ExpiraEn = expiraEn,
            IpOrigen = ip,
            FechaCreacion = clock.UtcNow
        };
        db.RefreshTokens.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task RevokeAsync(long id, long? replacementId, CancellationToken ct) =>
        await db.RefreshTokens.Where(x => x.Id == id && x.RevocadoEn == null).ExecuteUpdateAsync(
            setter => setter.SetProperty(x => x.RevocadoEn, clock.UtcNow).SetProperty(x => x.ReemplazadoPorId, replacementId), ct);

    public async Task RevokeFamilyAsync(Guid familiaId, CancellationToken ct) =>
        await db.RefreshTokens.Where(x => x.FamiliaId == familiaId && x.RevocadoEn == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.RevocadoEn, clock.UtcNow), ct);

    public async Task RevokeUserAsync(int usuarioId, CancellationToken ct) =>
        await db.RefreshTokens.Where(x => x.UsuarioId == usuarioId && x.RevocadoEn == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.RevocadoEn, clock.UtcNow), ct);

    public Task<bool> IsActiveAsync(long id, int usuarioId, CancellationToken ct) =>
        db.RefreshTokens.AsNoTracking().AnyAsync(x => x.Id == id && x.UsuarioId == usuarioId && x.RevocadoEn == null && x.ExpiraEn > clock.UtcNow, ct);
}
