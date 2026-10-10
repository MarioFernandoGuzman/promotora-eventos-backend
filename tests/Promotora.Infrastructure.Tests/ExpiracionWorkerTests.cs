using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;
using Promotora.Worker.Ticketing;

namespace Promotora.Infrastructure.Tests;

public sealed class ExpiracionWorkerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly DateTime ahora = new(2030, 2, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("commit")]
    [InlineData("rollback")]
    [InlineData("dispose")]
    public async Task LockNoEsperaYSeLiberaAlTerminarTransaccion(string fin)
    {
        await using var primero = postgres.CreateContext(); await using var segundo = postgres.CreateContext();
        await using var tx1 = await primero.Database.BeginTransactionAsync(Ct);
        await using var tx2 = await segundo.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfExpiracionCompraRepository(primero).IntentarCoordinarAsync(Ct));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        Assert.False(await new EfExpiracionCompraRepository(segundo).IntentarCoordinarAsync(timeout.Token));
        if (fin == "commit") await tx1.CommitAsync(Ct);
        else if (fin == "rollback") await tx1.RollbackAsync(Ct);
        else await tx1.DisposeAsync();
        Assert.True(await new EfExpiracionCompraRepository(segundo).IntentarCoordinarAsync(Ct));
    }

    [Fact]
    public async Task DosHostsOmitenSiHayLockYDespuesExpiranPorLotesSinDuplicar()
    {
        var uno = await CompraAsync("PENDIENTE", -2); var dos = await CompraAsync("PENDIENTE", -1);
        var vigente = await CompraAsync("PENDIENTE", 1); var procesando = await CompraAsync("PROCESANDO", -1);
        await using var bloqueo = postgres.CreateContext(); await using var tx = await bloqueo.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfExpiracionCompraRepository(bloqueo).IntentarCoordinarAsync(Ct));
        var estado = new EstadoPrueba();
        var host1 = Host(estado); var host2 = Host(estado);
        await using var lifetime1 = (IAsyncDisposable)host1; await using var lifetime2 = (IAsyncDisposable)host2;
        await host1.StartAsync(Ct); await host2.StartAsync(Ct);
        try
        {
            await EsperarAsync(() => Task.FromResult(Volatile.Read(ref estado.Ciclos) >= 2));
            await using var db = postgres.CreateContext();
            Assert.Equal("PENDIENTE", await EstadoAsync(db, uno.Id)); Assert.Equal("PENDIENTE", await EstadoAsync(db, dos.Id));
            Assert.Equal(2, await CupoAsync(db, uno.TipoId));
            await tx.CommitAsync(Ct);
            await EsperarAsync(async () => await EstadoAsync(db, uno.Id) == "EXPIRADA" && await EstadoAsync(db, dos.Id) == "EXPIRADA");
            foreach (var compra in new[] { uno, dos })
            {
                Assert.Equal(5, await CupoAsync(db, compra.TipoId));
                Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString() && a.Accion == "UPDATE", Ct));
            }
            Assert.Equal("PENDIENTE", await EstadoAsync(db, vigente.Id)); Assert.Equal(2, await CupoAsync(db, vigente.TipoId));
            Assert.Equal("PROCESANDO", await EstadoAsync(db, procesando.Id)); Assert.Equal(2, await CupoAsync(db, procesando.TipoId));
        }
        finally { await host1.StopAsync(Ct); await host2.StopAsync(Ct); }
    }

    [Fact]
    public async Task ShutdownDuranteAuditoriaRevierteCompraCupoYLiberaLock()
    {
        var compra = await CompraAsync("PENDIENTE", -1); var estado = new EstadoPrueba { Pausar = true };
        var host = Host(estado); await using var lifetime = (IAsyncDisposable)host;
        await host.StartAsync(Ct);
        try { await estado.EnAuditoria.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct); }
        finally { await host.StopAsync(Ct); }
        await using var db = postgres.CreateContext();
        Assert.Equal("PENDIENTE", await EstadoAsync(db, compra.Id)); Assert.Equal(2, await CupoAsync(db, compra.TipoId));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString(), Ct));
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfExpiracionCompraRepository(db).IntentarCoordinarAsync(Ct));
    }

    [Fact]
    public async Task FalloDelPrimerCicloSeReintentaConScopeNuevo()
    {
        var compra = await CompraAsync("PENDIENTE", -1); var estado = new EstadoPrueba { FallarPrimero = true };
        var host = Host(estado); await using var lifetime = (IAsyncDisposable)host;
        await host.StartAsync(Ct);
        try
        {
            await using var db = postgres.CreateContext();
            await EsperarAsync(async () => await EstadoAsync(db, compra.Id) == "EXPIRADA");
            Assert.True(Volatile.Read(ref estado.Auditorias) >= 2);
            Assert.Equal(5, await CupoAsync(db, compra.TipoId));
            Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString(), Ct));
        }
        finally { await host.StopAsync(Ct); }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1001, 1)]
    [InlineData(100, 0)]
    [InlineData(100, 301)]
    public void ConfiguracionInvalidaSeRechazaAntesDeArrancar(int lote, int intervalo)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddExpiracionWorker(new(lote, intervalo)));

    [Fact]
    public async Task CoordinacionExigeTransaccionYRespetaCancelacion()
    {
        await using var db = postgres.CreateContext(); var repo = new EfExpiracionCompraRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.IntentarCoordinarAsync(Ct));
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.IntentarCoordinarAsync(stop.Token));
    }

    private IHost Host(EstadoPrueba estado)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddPromotoraInfrastructure(postgres.ConnectionString);
        builder.Services.AddSingleton<IClock>(new RelojFijo(ahora));
        builder.Services.AddExpiracionCompras(); builder.Services.AddExpiracionWorker(new(1, 1));
        builder.Services.AddScoped<IExpiracionCompraRepository>(s => new RepoContado(new EfExpiracionCompraRepository(s.GetRequiredService<PromotoraDbContext>()), estado));
        builder.Services.AddScoped<IAuditLogger>(s => new AuditPrueba(new EfAuditLogger(s.GetRequiredService<PromotoraDbContext>(), s.GetRequiredService<IClock>()), estado));
        return builder.Build();
    }

    private async Task<(int Id, int TipoId)> CompraAsync(string estado, int minutos)
    {
        await using var db = postgres.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 3, Ct));
        var compra = new CompraRow
        {
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            Estado = estado,
            FechaCompra = ahora.AddHours(-1),
            ExpiraEn = ahora.AddMinutes(minutos),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ"
        };
        db.Compras.Add(compra); await db.SaveChangesAsync(Ct);
        db.DetalleCompra.Add(new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 3 });
        await db.SaveChangesAsync(Ct); await tx.CommitAsync(Ct); return (compra.Id, datos.TipoId);
    }

    private static Task<string> EstadoAsync(PromotoraDbContext db, int id) => db.Compras.Where(c => c.Id == id).Select(c => c.Estado).SingleAsync(Ct);
    private static Task<int> CupoAsync(PromotoraDbContext db, int id) => db.TiposEntrada.Where(t => t.Id == id).Select(t => t.CupoDisponible).SingleAsync(Ct);
    private static async Task EsperarAsync(Func<Task<bool>> condicion)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (!await condicion()) await Task.Delay(50, timeout.Token);
    }
    private sealed record RelojFijo(DateTime UtcNow) : IClock;
    private sealed class EstadoPrueba
    {
        public int Ciclos; public int Auditorias; public bool Pausar; public bool FallarPrimero;
        public TaskCompletionSource EnAuditoria { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class RepoContado(IExpiracionCompraRepository inner, EstadoPrueba estado) : IExpiracionCompraRepository
    {
        public async Task<bool> IntentarCoordinarAsync(CancellationToken ct)
        {
            bool tomado = await inner.IntentarCoordinarAsync(ct); Interlocked.Increment(ref estado.Ciclos); return tomado;
        }
        public Task<IReadOnlyList<int>> VencidasAsync(DateTime ahora, int lote, CancellationToken ct) => inner.VencidasAsync(ahora, lote, ct);
    }
    private sealed class AuditPrueba(IAuditLogger inner, EstadoPrueba estado) : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct)
            => WriteAsync(usuarioId, accion, entidad, entidadId, ip, null, ct);
        public async Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            int numero = Interlocked.Increment(ref estado.Auditorias);
            await inner.WriteAsync(usuarioId, accion, entidad, entidadId, ip, detalle, ct);
            if (estado.FallarPrimero && numero == 1) throw new InvalidOperationException("Fallo temporal de prueba.");
            if (estado.Pausar) { estado.EnAuditoria.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
        }
    }
}
