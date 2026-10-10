using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class IdentitySessionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly IdentityPasswordService Passwords = new();
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private static readonly SesionOptions Options = new();
    private static readonly JwtTokenService Tokens = new(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32), "tests", "tests", Clock);

    [Fact]
    public async Task LoginRotaRefreshYReutilizacionRevocaFamiliaIncluidoJwtNuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, email, password) = await CreateAsync(false, ct);
        await using var db = fixture.CreateContext();
        var repo = new EfSesionRepository(db, Clock);
        var usuario = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, Clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, Clock)));
        var audit = new EfAuditLogger(db, Clock);
        var login = new LoginHandler(usuario, repo, Passwords, Tokens, new Actor(id), Clock, Options, audit);
        var session = await login.HandleAsync(new LoginCommand(email, password), ct);
        Assert.True(session.IsSuccess);
        Assert.NotNull(session.Value.RefreshToken);
        var original = await repo.FindAsync(Tokens.HashRefreshToken(session.Value.RefreshToken), ct);
        Assert.NotNull(original);
        Assert.NotEqual(session.Value.RefreshToken, (await db.RefreshTokens.SingleAsync(x => x.Id == original.Id, ct)).TokenHash);
        var refresh = new RefreshHandler(usuario, repo, Tokens, new Actor(id), Clock, Options, audit);
        var rotated = await refresh.HandleAsync(new RefreshCommand(session.Value.RefreshToken), ct);
        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(session.Value.RefreshToken, rotated.Value.RefreshToken);
        var replacement = await repo.FindAsync(Tokens.HashRefreshToken(rotated.Value.RefreshToken!), ct);
        Assert.True(await repo.IsActiveAsync(replacement!.Id, id, ct));
        var replay = await refresh.HandleAsync(new RefreshCommand(session.Value.RefreshToken), ct);
        Assert.Equal(ErrorType.Unauthorized, replay.Errors[0].Type);
        Assert.False(await repo.IsActiveAsync(replacement.Id, id, ct));
        Assert.True(await db.LogsAuditoria.AnyAsync(x => x.EntidadAfectada == "refresh_tokens" && x.EntidadId == original.Id.ToString(), ct));
        Assert.Equal(replacement.Id, (await db.RefreshTokens.AsNoTracking().SingleAsync(x => x.Id == original.Id, ct)).ReemplazadoPorId);
    }

    [Fact]
    public async Task DosRefreshConcurrentesSoloUnoGanaYReutilizacionRevocaSuSucesor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, email, password) = await CreateAsync(false, ct);
        TokenDto session;
        await using (var db = fixture.CreateContext())
        {
            session = (await Login(db, id).HandleAsync(new LoginCommand(email, password), ct)).Value;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            var handler = new RefreshHandler(new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, Clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, Clock))),
                new EfSesionRepository(db, Clock), Tokens, new Actor(id), Clock, Options, new EfAuditLogger(db, Clock));
            return await handler.HandleAsync(new RefreshCommand(session.RefreshToken!), ct);
        }));
        Assert.Single(results, x => x.IsSuccess);
        Assert.Single(results, x => x.IsFailure);
        await using var check = fixture.CreateContext();
        Assert.Equal(2, await check.RefreshTokens.CountAsync(x => x.UsuarioId == id, ct));
        Assert.Equal(0, await check.RefreshTokens.CountAsync(x => x.UsuarioId == id && x.RevocadoEn == null, ct));
    }

    [Fact]
    public async Task PasswordObligatorioEmiteTokenLimitadoSinRefreshYFalloLoginSeAudita()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, email, password) = await CreateAsync(true, ct);
        await using var db = fixture.CreateContext();
        var login = Login(db, id);
        var restricted = await login.HandleAsync(new LoginCommand(email, password), ct);
        Assert.True(restricted.Value.DebeCambiarPassword);
        Assert.Null(restricted.Value.RefreshToken);
        var decoded = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(restricted.Value.AccessToken);
        Assert.Equal("true", decoded.Claims.Single(x => x.Type == "password_change_required").Value);
        var invalid = await login.HandleAsync(new LoginCommand(email, "incorrecta"), ct);
        Assert.Equal(ErrorType.Unauthorized, invalid.Errors[0].Type);
        Assert.True(await db.LogsAuditoria.AnyAsync(x => x.UsuarioId == id && x.Accion == "LOGIN_FAILED", ct));
        var unknown = await login.HandleAsync(new LoginCommand($"{Guid.NewGuid():N}@example.test", password), ct);
        Assert.Equal(invalid.Errors[0], unknown.Errors[0]);
    }

    [Fact]
    public async Task LogoutRechazaTokenAjenoYLuegoRevocaLaSesionPropia()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, email, password) = await CreateAsync(false, ct);
        await using var db = fixture.CreateContext();
        var session = (await Login(db, id).HandleAsync(new LoginCommand(email, password), ct)).Value;
        var repo = new EfSesionRepository(db, Clock);
        var denied = await new LogoutHandler(repo, Tokens, new Actor(null), new EfAuditLogger(db, Clock)).HandleAsync(new LogoutCommand(session.RefreshToken!), ct);
        Assert.True(denied.IsFailure);
        var own = await new LogoutHandler(repo, Tokens, new Actor(id), new EfAuditLogger(db, Clock)).HandleAsync(new LogoutCommand(session.RefreshToken!), ct);
        Assert.True(own.IsSuccess);
        Assert.Equal(0, await db.RefreshTokens.CountAsync(x => x.UsuarioId == id && x.RevocadoEn == null, ct));
    }

    private static LoginHandler Login(PromotoraDbContext db, int id) => new(
        new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, Clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, Clock))), new EfSesionRepository(db, Clock),
        Passwords, Tokens, new Actor(id), Clock, Options, new EfAuditLogger(db, Clock));

    private async Task<(int Id, string Email, string Password)> CreateAsync(bool mustChange, CancellationToken ct)
    {
        await using var db = fixture.CreateContext();
        var email = $"{Guid.NewGuid():N}@example.test";
        var password = Guid.NewGuid().ToString("N");
        var repo = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, Clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, Clock)));
        var user = await repo.CreateAsync(new Usuario(0, email, Passwords.Hash(password), "Nombre", "Apellido", RolUsuario.CLIENTE,
            "ACTIVO", mustChange, Clock.UtcNow), new PerfilNuevo(), null, null, false, ct);
        return (user.Value.Id, email, password);
    }

    private sealed class Actor(int? id) : IUsuarioActual
    {
        public int? UsuarioId => id;
        public string? IpOrigen => "127.0.0.1";
    }
}
