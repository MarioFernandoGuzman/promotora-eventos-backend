using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Validation;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class IdentityRegistrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly IdentityPasswordService Passwords = new();
    private static readonly SystemClock Clock = new(TimeProvider.System);

    [Fact]
    public async Task RegistroNormalizaCorreoCreaPerfilAuditoriaOutboxYRechazaDuplicado()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = Guid.NewGuid().ToString("N");
        var email = $"{Guid.NewGuid():N}@example.test";
        await using var db = fixture.CreateContext();
        var handler = CreateHandler(db, new Actor(null));
        var request = new CrearUsuarioCommand(" " + email.ToUpperInvariant() + " ", password, "Cliente", "Prueba", RolUsuario.CLIENTE, new PerfilNuevo());
        // La validación admite espacios externos solo tras normalizar en el handler; correo sin espacios para validator.
        var result = await handler.HandleAsync(request with { Email = email.ToUpperInvariant() }, ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(email, result.Value.Email);
        var row = await db.Usuarios.AsNoTracking().SingleAsync(x => x.Id == result.Value.Id, ct);
        Assert.NotEqual(password, row.PasswordHash);
        Assert.True(Passwords.Verify(row.PasswordHash, password));
        Assert.False(Passwords.Verify(row.PasswordHash, "incorrecta"));
        Assert.True(await db.Clientes.AnyAsync(x => x.UsuarioId == row.Id, ct));
        Assert.True(await db.LogsAuditoria.AnyAsync(x => x.EntidadId == row.Id.ToString() && x.Accion == "CREATE", ct));
        Assert.True(await db.OutboxMensajes.AnyAsync(x => x.AgregadoId == row.Id.ToString() && x.Tipo == "usuario.registrado", ct));
        var duplicate = await handler.HandleAsync(request with { Email = email }, ct);
        Assert.True(duplicate.IsFailure);
        Assert.Equal(ErrorType.Conflict, duplicate.Errors[0].Type);
        Assert.Equal(1, await db.Usuarios.CountAsync(x => x.Email == email, ct));
    }

    [Fact]
    public async Task BootstrapConcurrenteCreaUnSuperadminYNoCambiaSuPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var requests = Enumerable.Range(0, 3).Select(async index =>
        {
            await using var db = fixture.CreateContext();
            var handler = new BootstrapAdminHandler(Repository(db), Passwords, Clock);
            return await handler.HandleAsync(new BootstrapAdminCommand($"bootstrap{index}@example.test", Guid.NewGuid().ToString("N")), ct);
        });
        var results = await Task.WhenAll(requests);
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results.Select(result => result.Value.Id).Distinct());
        await using var check = fixture.CreateContext();
        Assert.Equal(1, await check.Administradores.CountAsync(x => x.NivelAcceso == "SUPERADMIN", ct));
        Assert.True((await check.Usuarios.SingleAsync(x => x.Id == results[0].Value.Id, ct)).DebeCambiarPassword);
        var adminId = results[0].Value.Id.ToString();
        Assert.Equal(1, await check.LogsAuditoria.CountAsync(x => x.Accion == "CREATE" && x.EntidadAfectada == "usuarios" && x.EntidadId == adminId, ct));
    }

    [Fact]
    public async Task AltaPrivilegiadaRequiereAdministradorActivoYPerfilAcorde()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext();
        var request = new CrearUsuarioCommand($"{Guid.NewGuid():N}@example.test", Guid.NewGuid().ToString("N"),
            "Dueño", "Prueba", RolUsuario.DUENO_RECINTO, new PerfilNuevo(Empresa: "Empresa"));
        var denied = await CreateHandler(db, new Actor(null)).HandleAsync(request, ct);
        Assert.Equal(ErrorType.Forbidden, denied.Errors[0].Type);
        var repo = Repository(db);
        var admin = await repo.CreateAsync(new Usuario(0, $"{Guid.NewGuid():N}@example.test", Passwords.Hash(Guid.NewGuid().ToString("N")),
            "Admin", "Prueba", RolUsuario.ADMINISTRADOR, "ACTIVO", false, Clock.UtcNow), new PerfilNuevo(), null, null, false, ct);
        var created = await CreateHandler(db, new Actor(admin.Value.Id)).HandleAsync(request, ct);
        Assert.True(created.IsSuccess);
        Assert.True(await db.DuenosRecinto.AnyAsync(x => x.UsuarioId == created.Value.Id && x.Empresa == "Empresa", ct));
        var forged = await CreateHandler(db, new Actor(created.Value.Id)).HandleAsync(request with { Email = "forged@example.test", Rol = RolUsuario.ADMINISTRADOR }, ct);
        Assert.Equal(ErrorType.Forbidden, forged.Errors[0].Type);
    }

    [Fact]
    public async Task ValidacionRechazaPasswordCortoYNivelSuperadminEnAltaNormal()
    {
        await using var db = fixture.CreateContext();
        var result = await CreateHandler(db, new Actor(null)).HandleAsync(new CrearUsuarioCommand("bad@example.test", "corta",
            "Nombre", "Apellido", RolUsuario.ADMINISTRADOR, new PerfilNuevo(NivelAcceso: "SUPERADMIN")), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.False(Passwords.Verify("!SIN_ACCESO", "cualquiera"));
        Assert.NotEqual(Passwords.Hash("password-de-prueba"), Passwords.Hash("password-de-prueba"));
    }

    private static EfUsuarioRepository Repository(PromotoraDbContext db) => new(db, new EfUnitOfWork(db), new EfAuditLogger(db, Clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, Clock)));
    private static ICommandHandler<CrearUsuarioCommand, UsuarioDto> CreateHandler(PromotoraDbContext db, Actor actor) =>
        new ValidatingCommandHandler<CrearUsuarioCommand, UsuarioDto>(new CrearUsuarioHandler(Repository(db), Passwords, actor, Clock), [new CrearUsuarioValidator()]);

    private sealed class Actor(int? id) : IUsuarioActual
    {
        public int? UsuarioId => id;
        public string? IpOrigen => "127.0.0.1";
    }
}
