using Microsoft.EntityFrameworkCore;
using Promotora.Application.Audit;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class IdentityAuditTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AuditoriaJsonAcotadaPersisteConUtcYSinDatosDeCredenciales()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var audit = new EfAuditLogger(db, new SystemClock(TimeProvider.System));
        var family = Guid.NewGuid();
        var id = Guid.NewGuid().ToString("N");
        await new EfUnitOfWork(db).ExecuteInTransactionAsync(async token =>
        {
            await audit.WriteAsync(null, "LOGOUT", "refresh_tokens", id, "127.0.0.1", new DetalleAuditoria(MotivoAuditoria.RefreshReutilizado, family), token);
            return Result<Unit>.Success(Unit.Value);
        }, ct);
        var row = await db.LogsAuditoria.AsNoTracking().SingleAsync(x => x.EntidadId == id, ct);
        Assert.Equal(DateTimeKind.Utc, row.FechaHora.Kind);
        Assert.Equal("127.0.0.1", row.IpOrigen);
        Assert.Equal("RefreshReutilizado", row.Detalle!.RootElement.GetProperty("motivo").GetString());
        Assert.Equal(family, row.Detalle.RootElement.GetProperty("familiaId").GetGuid());
        Assert.Equal(2, row.Detalle.RootElement.EnumerateObject().Count());
        Assert.Null(row.UsuarioId);
    }

    [Fact]
    public async Task FalloDeTransaccionRevierteTambienAuditoriaInclusoGuardadaPreviamente()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var id = Guid.NewGuid().ToString("N");
        var audit = new EfAuditLogger(db, new SystemClock(TimeProvider.System));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfUnitOfWork(db).ExecuteInTransactionAsync<Unit>(async token =>
        {
            await audit.WriteAsync(null, "CREATE", "usuarios", id, null, new DetalleAuditoria(MotivoAuditoria.AltaUsuario), token);
            await db.SaveChangesAsync(token);
            throw new InvalidOperationException("Fallo simulado.");
        }, ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(x => x.EntidadId == id, ct));
    }
}
