using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class ExpiracionComprasTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly DateTime _ahora = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SoloExpiraPendientesVencidasRespetaLoteYRepetirNoDevuelveMasCupo()
    {
        var ct = TestContext.Current.CancellationToken;
        var vieja = await PrepararAsync("PENDIENTE", -2, ct);
        var limite = await PrepararAsync("PENDIENTE", 0, ct);
        var futura = await PrepararAsync("PENDIENTE", 1, ct);
        var procesando = await PrepararAsync("PROCESANDO", -1, ct);
        var completada = await PrepararAsync("COMPLETADA", -1, ct);
        await using var db = fixture.CreateContext(); var handler = Handler(db);
        var primero = await handler.HandleAsync(new(1), ct); Assert.True(primero.IsSuccess); Assert.Equal(new(1, 1), primero.Value);
        Assert.Equal("EXPIRADA", await EstadoAsync(db, vieja.Id, ct)); Assert.Equal("PENDIENTE", await EstadoAsync(db, limite.Id, ct));
        var segundo = await handler.HandleAsync(new(), ct); Assert.Equal(1, segundo.Value.Expiradas);
        Assert.Equal(0, (await handler.HandleAsync(new(), ct)).Value.Expiradas);
        Assert.Equal("PENDIENTE", await EstadoAsync(db, futura.Id, ct)); Assert.Equal("PROCESANDO", await EstadoAsync(db, procesando.Id, ct));
        Assert.Equal("COMPLETADA", await EstadoAsync(db, completada.Id, ct));
        foreach (var dato in new[] { vieja, limite })
        {
            Assert.Equal(5, await CupoAsync(db, dato.TipoId, ct));
            var audit = Assert.Single(await db.LogsAuditoria.Where(a => a.EntidadAfectada == "compras" && a.EntidadId == dato.Id.ToString() && a.Accion == "UPDATE").ToArrayAsync(ct));
            Assert.Null(audit.UsuarioId); Assert.Equal(_ahora, audit.FechaHora);
        }
        foreach (var dato in new[] { futura, procesando, completada }) Assert.Equal(2, await CupoAsync(db, dato.TipoId, ct));
    }

    [Fact]
    public async Task DiezHandlersConcurrentesExpiranCadaCompraUnaSolaVez()
    {
        var ct = TestContext.Current.CancellationToken;
        var uno = await PrepararAsync("PENDIENTE", -1, ct); var dos = await PrepararAsync("PENDIENTE", -1, ct);
        var resultados = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = fixture.CreateContext(); return await Handler(db).HandleAsync(new(), ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess)); Assert.Equal(2, resultados.Sum(r => r.Value.Expiradas));
        await using var verificar = fixture.CreateContext();
        foreach (var dato in new[] { uno, dos })
        {
            Assert.Equal("EXPIRADA", await EstadoAsync(verificar, dato.Id, ct)); Assert.Equal(5, await CupoAsync(verificar, dato.TipoId, ct));
            Assert.Equal(1, await verificar.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == dato.Id.ToString() && a.Accion == "UPDATE", ct));
        }
    }

    [Fact]
    public async Task CancelarOProcesarEntreConsultaYUpdateImpideOtraDevolucion()
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (bool cancelar in new[] { true, false })
        {
            var dato = await PrepararAsync("PENDIENTE", -1, ct);
            await using var db = fixture.CreateContext();
            var repo = new ConsultaConCarrera(new EfExpiracionCompraRepository(db), async token =>
            {
                await using var otro = fixture.CreateContext(); await using var tx = await otro.Database.BeginTransactionAsync(token);
                if (cancelar) Assert.True(await new EfCupoEntradaRepository(otro).FinalizarYDevolverAsync(dato.Id, EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, _ahora, token));
                else Assert.Equal(1, await otro.Compras.Where(c => c.Id == dato.Id && c.Estado == "PENDIENTE")
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "PROCESANDO").SetProperty(c => c.ProcesandoDesde, _ahora), token));
                await tx.CommitAsync(token);
            });
            var resultado = await Handler(db, repo: repo).HandleAsync(new(), ct);
            Assert.True(resultado.IsSuccess); Assert.Equal(0, resultado.Value.Expiradas);
            Assert.Equal(cancelar ? "CANCELADA" : "PROCESANDO", await EstadoAsync(db, dato.Id, ct));
            Assert.Equal(cancelar ? 5 : 2, await CupoAsync(db, dato.TipoId, ct));
        }
    }

    [Fact]
    public async Task FalloDeAuditoriaRevierteTodoElLoteYCupo()
    {
        var ct = TestContext.Current.CancellationToken;
        var uno = await PrepararAsync("PENDIENTE", -2, ct); var dos = await PrepararAsync("PENDIENTE", -1, ct);
        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(db, new AuditFallando()).HandleAsync(new(), ct));
        foreach (var dato in new[] { uno, dos }) { Assert.Equal("PENDIENTE", await EstadoAsync(db, dato.Id, ct)); Assert.Equal(2, await CupoAsync(db, dato.TipoId, ct)); }
        Assert.Equal(2, (await Handler(db).HandleAsync(new(), ct)).Value.Expiradas);
    }

    [Fact]
    public async Task WorkerResuelveHandlerSinActorHttpYLoteInvalidoOCanceladoNoEscribe()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(fixture.ConnectionString); services.AddExpiracionCompras();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope(); var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ExpirarComprasCommand, ExpiracionLoteDto>>();
        Assert.True((await handler.HandleAsync(new(0), ct)).IsFailure); Assert.True((await handler.HandleAsync(new(1001), ct)).IsFailure);
        using var cancelado = new CancellationTokenSource(); await cancelado.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(new(), cancelado.Token));
        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfExpiracionCompraRepository(db).VencidasAsync(_ahora, 100, ct));
    }

    private ExpirarComprasHandler Handler(PromotoraDbContext db, IAuditLogger? audit = null, IExpiracionCompraRepository? repo = null)
    {
        var clock = new Fijo(_ahora);
        return new(repo ?? new EfExpiracionCompraRepository(db), new EfCupoEntradaRepository(db), new EfTicketingTransaction(new EfUnitOfWork(db)), clock, audit ?? new EfAuditLogger(db, clock));
    }
    private async Task<(int Id, int TipoId)> PrepararAsync(string estado, int minutos, CancellationToken ct)
    {
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 3, ct));
        var compra = new CompraRow
        {
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            Estado = estado,
            FechaCompra = _ahora.AddHours(-1),
            ExpiraEn = _ahora.AddMinutes(minutos),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ"
        };
        db.Compras.Add(compra); await db.SaveChangesAsync(ct);
        db.DetalleCompra.Add(new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 3 });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return (compra.Id, datos.TipoId);
    }
    private static Task<string> EstadoAsync(PromotoraDbContext db, int id, CancellationToken ct) => db.Compras.Where(c => c.Id == id).Select(c => c.Estado).SingleAsync(ct);
    private static Task<int> CupoAsync(PromotoraDbContext db, int id, CancellationToken ct) => db.TiposEntrada.Where(t => t.Id == id).Select(t => t.CupoDisponible).SingleAsync(ct);
    private sealed record Fijo(DateTime UtcNow) : IClock;
    private sealed class ConsultaConCarrera(IExpiracionCompraRepository repo, Func<CancellationToken, Task> carrera) : IExpiracionCompraRepository
    {
        public async Task<IReadOnlyList<int>> VencidasAsync(DateTime ahora, int lote, CancellationToken ct)
        { var ids = await repo.VencidasAsync(ahora, lote, ct); await carrera(ct); return ids; }
    }
    private sealed class AuditFallando : IAuditLogger
    {
        private int _escrituras;
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct)
        { if (++_escrituras == 2) throw new InvalidOperationException("Fallo de auditoría de prueba."); return Task.CompletedTask; }
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => WriteAsync(usuarioId, accion, entidad, entidadId, ip, ct);
    }
}
