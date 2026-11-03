using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Audit;
using Promotora.Application.Finance;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Registration;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class CuentasFinancierasTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);

    [Fact]
    public async Task AltaDeDuenoCreaCuentaYAuditoriaEnLaMismaTransaccion()
    {
        var usuario = await CrearAsync(RolUsuario.DUENO_RECINTO); await using var db = fixture.CreateContext();
        var cuenta = await CuentaAsync(db, usuario.Id);
        Assert.Equal("ACTIVA", cuenta.Estado); Assert.Equal(0m, cuenta.Saldo); Assert.Equal("GTQ", cuenta.Moneda);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "cuentas_financieras" && a.EntidadId == cuenta.Id.ToString() && a.Accion == "CREATE", Ct));
        foreach (var tipo in new[] { "PLATAFORMA", "IMPUESTOS" }) Assert.Equal(1, await db.CuentasFinancieras.CountAsync(c => c.Tipo == tipo && c.Moneda == "GTQ", Ct));
    }

    [Fact]
    public async Task FalloDeAuditoriaDeCuentaRevierteUsuarioPerfilYCuenta()
    {
        await using var db = fixture.CreateContext(); string email = $"{Guid.NewGuid():N}@example.invalid";
        var audit = new AuditFallando(new EfAuditLogger(db, Clock));
        var repo = new EfUsuarioRepository(db, new EfUnitOfWork(db), audit, new EfCuentaRepository(db, audit));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CreateAsync(Usuario(email, RolUsuario.DUENO_RECINTO), new PerfilNuevo(Empresa: "Prueba"), null, null, false, Ct));
        Assert.False(await db.Usuarios.AnyAsync(u => u.Email == email, Ct));
    }

    [Fact]
    public async Task VeinteAltasIdempotentesNoDuplicanNiReabrenUnaCuenta()
    {
        var usuario = await CrearAsync(RolUsuario.DUENO_RECINTO);
        await using var db = fixture.CreateContext(); var cuenta = await CuentaAsync(db, usuario.Id);
        Assert.True((await Handler(db, usuario.Id).HandleAsync(new(cuenta.Id), Ct)).IsSuccess);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var contexto = fixture.CreateContext(); var repo = new EfCuentaRepository(contexto, new EfAuditLogger(contexto, Clock));
            await new EfUnitOfWork(contexto).ExecuteInTransactionAsync(async token =>
            {
                await repo.CrearEnTransaccionAsync(cuenta.DuenoId!.Value, null, null, token); return Result<Unit>.Success(Unit.Value);
            }, Ct);
        }));
        Assert.Equal(1, await db.CuentasFinancieras.CountAsync(c => c.DuenoId == cuenta.DuenoId && c.Moneda == "GTQ", Ct));
        Assert.Equal("CERRADA", (await db.CuentasFinancieras.AsNoTracking().SingleAsync(c => c.Id == cuenta.Id, Ct)).Estado);
    }

    [Fact]
    public async Task VeinteCierresConcurrentesConservanUnaAuditoriaYFecha()
    {
        var usuario = await CrearAsync(RolUsuario.DUENO_RECINTO);
        await using var db = fixture.CreateContext(); var cuenta = await CuentaAsync(db, usuario.Id);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var contexto = fixture.CreateContext(); return await Handler(contexto, usuario.Id).HandleAsync(new(cuenta.Id), Ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess)); Assert.Single(resultados.Select(r => r.Value.FechaCierre).Distinct());
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "cuentas_financieras" && a.EntidadId == cuenta.Id.ToString() && a.Accion == "UPDATE", Ct));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task SaldoNoCeroNoSeCierra(int monto)
    {
        var usuario = await CrearAsync(RolUsuario.DUENO_RECINTO); await using var db = fixture.CreateContext(); var cuenta = await CuentaAsync(db, usuario.Id);
        await SaldoAsync(db, cuenta.Id, monto);
        var resultado = await Handler(db, usuario.Id).HandleAsync(new(cuenta.Id), Ct);
        Assert.Equal("cuenta.saldo", Assert.Single(resultado.Errors).Code);
    }

    [Fact]
    public async Task ClienteODuenoAjenoNoPuedeCerrarCuentaYAdminNoCierraSistema()
    {
        var propio = await CrearAsync(RolUsuario.DUENO_RECINTO); var ajeno = await CrearAsync(RolUsuario.DUENO_RECINTO);
        var cliente = await CrearAsync(RolUsuario.CLIENTE); var admin = await CrearAsync(RolUsuario.ADMINISTRADOR);
        await using var db = fixture.CreateContext(); var cuenta = await CuentaAsync(db, propio.Id);
        Assert.Equal(ErrorType.NotFound, Assert.Single((await Handler(db, ajeno.Id).HandleAsync(new(cuenta.Id), Ct)).Errors).Type);
        Assert.Equal(ErrorType.Forbidden, Assert.Single((await Handler(db, cliente.Id).HandleAsync(new(cuenta.Id), Ct)).Errors).Type);
        foreach (var tipo in new[] { "PLATAFORMA", "IMPUESTOS" })
        {
            int id = await db.CuentasFinancieras.Where(c => c.Tipo == tipo && c.Moneda == "GTQ").Select(c => c.Id).SingleAsync(Ct);
            Assert.Equal("cuenta.tipo", Assert.Single((await Handler(db, admin.Id).HandleAsync(new(id), Ct)).Errors).Code);
        }
        Assert.True((await Handler(db, admin.Id).HandleAsync(new(cuenta.Id), Ct)).IsSuccess);
    }

    [Fact]
    public async Task FalloDeAuditoriaRevierteCierreYFecha()
    {
        var usuario = await CrearAsync(RolUsuario.DUENO_RECINTO); await using var db = fixture.CreateContext(); var cuenta = await CuentaAsync(db, usuario.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(db, usuario.Id, new AuditFallando(new EfAuditLogger(db, Clock))).HandleAsync(new(cuenta.Id), Ct));
        var actual = await db.CuentasFinancieras.AsNoTracking().SingleAsync(c => c.Id == cuenta.Id, Ct);
        Assert.Equal("ACTIVA", actual.Estado); Assert.Null(actual.FechaCierre);
    }

    [Fact]
    public async Task TriggerRechazaMovimientoEnCuentaCerrada()
    {
        var usuario = await CrearAsync(RolUsuario.DUENO_RECINTO); await using var db = fixture.CreateContext(); var cuenta = await CuentaAsync(db, usuario.Id);
        Assert.True((await Handler(db, usuario.Id).HandleAsync(new(cuenta.Id), Ct)).IsSuccess);
        db.MovimientosFinancieros.Add(new MovimientoFinancieroRow { CuentaId = cuenta.Id, Tipo = "LIQUIDACION", Monto = -1m });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal("23514", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    private CerrarCuentaHandler Handler(PromotoraDbContext db, int actorId, IAuditLogger? audit = null)
    {
        audit ??= new EfAuditLogger(db, Clock); var cuentas = new EfCuentaRepository(db, audit);
        return new(cuentas, new Actor(actorId), new EfUsuarioRepository(db, new EfUnitOfWork(db), audit, cuentas),
            new EfSesionRepository(db, Clock), new EfUnitOfWork(db), Clock, audit);
    }
    private async Task<UsuarioDto> CrearAsync(RolUsuario rol)
    {
        await using var db = fixture.CreateContext(); var audit = new EfAuditLogger(db, Clock);
        var repo = new EfUsuarioRepository(db, new EfUnitOfWork(db), audit, new EfCuentaRepository(db, audit));
        var result = await repo.CreateAsync(Usuario($"{Guid.NewGuid():N}@example.invalid", rol), new PerfilNuevo(Empresa: rol == RolUsuario.DUENO_RECINTO ? "Prueba" : null), null, null, false, Ct);
        Assert.True(result.IsSuccess); return result.Value;
    }
    private static Usuario Usuario(string email, RolUsuario rol) => new(0, email, "!SIN_ACCESO_PRUEBA!", "Prueba", "Finanzas", rol, "ACTIVO", false, Clock.UtcNow);
    private static Task<CuentaFinancieraRow> CuentaAsync(PromotoraDbContext db, int usuarioId) => db.CuentasFinancieras.AsNoTracking()
        .SingleAsync(c => c.DuenoId == db.DuenosRecinto.Where(d => d.UsuarioId == usuarioId).Select(d => d.Id).Single(), Ct);
    private static async Task SaldoAsync(PromotoraDbContext db, int id, int monto)
    {
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        db.MovimientosFinancieros.Add(new MovimientoFinancieroRow { CuentaId = id, Tipo = "LIQUIDACION", Monto = monto });
        await db.SaveChangesAsync(Ct);
        await db.CuentasFinancieras.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, monto), Ct); await tx.CommitAsync(Ct);
    }
    private sealed record Actor(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
    private sealed class AuditFallando(IAuditLogger inner) : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct)
            => WriteAsync(usuarioId, accion, entidad, entidadId, ip, null, ct);
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
            => entidad == "cuentas_financieras" ? throw new InvalidOperationException("Fallo de auditoría de prueba.") : inner.WriteAsync(usuarioId, accion, entidad, entidadId, ip, detalle, ct);
    }
}
