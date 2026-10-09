using Microsoft.EntityFrameworkCore;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class UnitOfWorkTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ExitoConfirmaYFalloRevierteInclusoGuardadoPrevio()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = Guid.NewGuid().ToString("N");
        await using var db = fixture.CreateContext();
        var uow = new EfUnitOfWork(db);
        var result = await uow.ExecuteInTransactionAsync(async token =>
        {
            db.CategoriasEvento.Add(new CategoriaEventoRow { Nombre = name });
            await uow.SaveChangesAsync(token);
            return Result<Unit>.Failure(new Error("prueba", "Revertir", ErrorType.BusinessRule));
        }, ct);
        Assert.True(result.IsFailure);
        await using var verification = fixture.CreateContext();
        Assert.False(await verification.CategoriasEvento.AnyAsync(c => c.Nombre == name, ct));
        result = await uow.ExecuteInTransactionAsync(token =>
        {
            token.ThrowIfCancellationRequested();
            db.CategoriasEvento.Add(new CategoriaEventoRow { Nombre = name });
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }, ct);
        Assert.True(result.IsSuccess);
        Assert.True(await verification.CategoriasEvento.AnyAsync(c => c.Nombre == name, ct));
        // Base temporal: se conserva el catálogo de producción para las otras pruebas.
        await verification.CategoriasEvento.Where(c => c.Nombre == name).ExecuteDeleteAsync(ct);
    }

    [Fact]
    public async Task ExcepcionRevierteTransaccion()
    {
        var ct = TestContext.Current.CancellationToken;
        var name = Guid.NewGuid().ToString("N");
        await using var db = fixture.CreateContext();
        var uow = new EfUnitOfWork(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync<Unit>(async token =>
        {
            db.CategoriasEvento.Add(new CategoriaEventoRow { Nombre = name });
            await db.SaveChangesAsync(token);
            throw new InvalidOperationException("Prueba de rollback");
        }, ct));
        await using var verification = fixture.CreateContext();
        Assert.False(await verification.CategoriasEvento.AnyAsync(c => c.Nombre == name, ct));
    }

    [Fact]
    public void RelojUsaUtcConProveedorInyectado()
    {
        var expected = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));
        var clock = new SystemClock(new FixedTimeProvider(expected));
        Assert.Equal(expected.UtcDateTime, clock.UtcNow);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
