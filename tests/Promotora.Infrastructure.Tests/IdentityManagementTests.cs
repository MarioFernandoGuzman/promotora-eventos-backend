using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Management;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class IdentityManagementTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private static readonly IdentityPasswordService Passwords = new();

    [Fact]
    public async Task EdicionPropiaActualizaPerfilYRechazaDatosAjenosOCorreoDuplicado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, _) = await CreateAsync(false, ct);
        var (other, _) = await CreateAsync(false, ct);
        await using var db = fixture.CreateContext();
        var repos = Repository(db);
        var sessions = new EfSesionRepository(db, Clock);
        var actor = new Actor(owner.Id);
        var access = new UsuarioAccess(repos, actor, sessions);
        var handler = new ActualizarUsuarioHandler(repos, sessions, actor, access, new EfAuditLogger(db, Clock));
        var edit = new ActualizarUsuarioCommand(owner.Id, owner.Email, "Nuevo", "Apellido", new PerfilNuevo(Telefono: "12345"));
        var result = await handler.HandleAsync(edit, ct);
        Assert.Equal("Nuevo", result.Value.Usuario.Nombre);
        Assert.Equal("12345", result.Value.Perfil.Telefono);
        var denied = await handler.HandleAsync(edit with { Id = other.Id }, ct);
        Assert.Equal(ErrorType.Forbidden, denied.Errors[0].Type);
        var duplicate = await handler.HandleAsync(edit with { Email = other.Email }, ct);
        Assert.Equal(ErrorType.Conflict, duplicate.Errors[0].Type);
        Assert.Equal(owner.Email, (await repos.FindByIdAsync(owner.Id, ct))!.Email);
        var read = new ObtenerUsuarioHandler(repos, access);
        Assert.True((await read.HandleAsync(new ObtenerUsuarioQuery(owner.Id), ct)).IsSuccess);
        Assert.Equal(ErrorType.Forbidden, (await read.HandleAsync(new ObtenerUsuarioQuery(other.Id), ct)).Errors[0].Type);
    }

    [Fact]
    public async Task CambioPasswordObligatorioExigeActualYRevocaTodasLasSesiones()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, oldPassword) = await CreateAsync(true, ct);
        var (other, _) = await CreateAsync(false, ct);
        await using var db = fixture.CreateContext();
        var repos = Repository(db);
        var sessions = new EfSesionRepository(db, Clock);
        var actor = new Actor(owner.Id);
        await sessions.ExecuteAtomicAsync(async token =>
        {
            await sessions.AddAsync(owner.Id, Guid.NewGuid().ToString("N"), Guid.NewGuid(), Clock.UtcNow.AddDays(1), null, token);
            return Result<Unit>.Success(Unit.Value);
        }, ct);
        var handler = new CambiarPasswordHandler(repos, sessions, actor, new UsuarioAccess(repos, actor, sessions), Passwords, new EfAuditLogger(db, Clock));
        var next = Guid.NewGuid().ToString("N");
        Assert.Equal(ErrorType.Forbidden, (await handler.HandleAsync(new CambiarPasswordCommand(other.Id, oldPassword, next), ct)).Errors[0].Type);
        Assert.Equal(ErrorType.Unauthorized, (await handler.HandleAsync(new CambiarPasswordCommand(owner.Id, "incorrecta", next), ct)).Errors[0].Type);
        Assert.Equal(ErrorType.BusinessRule, (await handler.HandleAsync(new CambiarPasswordCommand(owner.Id, oldPassword, oldPassword), ct)).Errors[0].Type);
        Assert.True((await handler.HandleAsync(new CambiarPasswordCommand(owner.Id, oldPassword, next), ct)).IsSuccess);
        var updated = (await repos.FindByIdAsync(owner.Id, ct))!;
        Assert.False(updated.DebeCambiarPassword);
        Assert.True(Passwords.Verify(updated.PasswordHash, next));
        Assert.False(Passwords.Verify(updated.PasswordHash, oldPassword));
        Assert.Equal(0, await db.RefreshTokens.CountAsync(x => x.UsuarioId == owner.Id && x.RevocadoEn == null, ct));
    }

    [Fact]
    public async Task BajaConservaUsuarioPerfilYAuditoriaYAdministradorPuedeListar()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, _) = await CreateAsync(false, ct);
        await using var db = fixture.CreateContext();
        var repos = Repository(db);
        var admin = await repos.CreateAsync(new Usuario(0, $"{Guid.NewGuid():N}@example.test", Passwords.Hash(Guid.NewGuid().ToString("N")),
            "Admin", "Prueba", RolUsuario.ADMINISTRADOR, "ACTIVO", false, Clock.UtcNow), new PerfilNuevo(), null, null, false, ct);
        var actor = new Actor(admin.Value.Id);
        var sessions = new EfSesionRepository(db, Clock);
        var access = new UsuarioAccess(repos, actor, sessions);
        var handler = new BajaUsuarioHandler(repos, sessions, actor, access, new EfAuditLogger(db, Clock));
        Assert.True((await handler.HandleAsync(new BajaUsuarioCommand(target.Id), ct)).IsSuccess);
        Assert.True((await handler.HandleAsync(new BajaUsuarioCommand(target.Id), ct)).IsSuccess);
        Assert.Equal("INACTIVO", (await repos.FindByIdAsync(target.Id, ct))!.Estado);
        Assert.True(await db.Clientes.AnyAsync(x => x.UsuarioId == target.Id, ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadId == target.Id.ToString() && x.Accion == "DELETE", ct));
        var listed = await new ListarUsuariosHandler(repos, access).HandleAsync(new ListarUsuariosQuery(1, 1), ct);
        Assert.Single(listed.Value.Items);
        Assert.True(listed.Value.Total >= 2);
        var inactiveActor = new Actor(target.Id);
        var forbidden = await new ObtenerUsuarioHandler(repos, new UsuarioAccess(repos, inactiveActor, sessions)).HandleAsync(new ObtenerUsuarioQuery(target.Id), ct);
        Assert.Equal(ErrorType.Forbidden, forbidden.Errors[0].Type);
    }

    private static EfUsuarioRepository Repository(PromotoraDbContext db) => new(db, new EfUnitOfWork(db), new EfAuditLogger(db, Clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, Clock)));

    private async Task<(UsuarioDto Usuario, string Password)> CreateAsync(bool mustChange, CancellationToken ct)
    {
        await using var db = fixture.CreateContext();
        var password = Guid.NewGuid().ToString("N");
        var result = await Repository(db).CreateAsync(new Usuario(0, $"{Guid.NewGuid():N}@example.test", Passwords.Hash(password),
            "Nombre", "Apellido", RolUsuario.CLIENTE, "ACTIVO", mustChange, Clock.UtcNow), new PerfilNuevo(), null, null, false, ct);
        return (result.Value, password);
    }

    private sealed class Actor(int id) : IUsuarioActual
    {
        public int? UsuarioId => id;
        public string? IpOrigen => "127.0.0.1";
    }
}
