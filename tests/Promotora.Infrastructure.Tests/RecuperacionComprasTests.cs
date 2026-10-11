using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;
using Promotora.Worker.Ticketing;

namespace Promotora.Infrastructure.Tests;

public sealed class RecuperacionComprasTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    [Theory]
    [InlineData("APROBADO")]
    [InlineData("RECHAZADO")]
    [InlineData("AUSENTE")]
    public async Task DosRecuperadoresConPasarelaNuevaFinalizanUnaVezSinVolverACobrar(string resultado)
    {
        var compra = await PrepararAsync();
        if (resultado != "AUSENTE") await Gateway(resultado == "RECHAZADO" ? 1m : 0m).CobrarAsync(new(compra.PublicId, 246.40m, "GTQ"), Ct);
        await using var provider = Servicios(new SoloConsulta(Gateway(1m)));
        var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct);
        }));
        Assert.All(resultados, r => Assert.True(r.IsSuccess));
        Assert.Equal(1, resultados.Sum(r => r.Value.Completadas + r.Value.Fallidas));
        await VerificarAsync(compra, resultado == "APROBADO");
        await using var nuevo = Servicios(new SoloConsulta(Gateway()));
        await using var siguiente = nuevo.CreateAsyncScope();
        var repetida = (await siguiente.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct)).Value;
        Assert.Equal(0, repetida.Completadas + repetida.Fallidas);
    }

    [Fact]
    public async Task FalloDespuesDelLedgerRevierteNegocioPeroConservaResultadoDurableParaReintento()
    {
        var compra = await PrepararAsync(); await Gateway().CobrarAsync(new(compra.PublicId, 246.40m, "GTQ"), Ct);
        await using (var falla = Servicios(new SoloConsulta(Gateway()), fallar: true))
        await using (var scope = falla.CreateAsyncScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct));
        await using var db = postgres.CreateContext();
        Assert.Equal("PROCESANDO", await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == compra.Id, Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == compra.MensajeId, Ct));
        Assert.Equal("APROBADO", (await Gateway(1m).ConsultarAsync(compra.PublicId, Ct))!.Estado);
        await using var correcto = Servicios(new SoloConsulta(Gateway(1m))); await using var nuevo = correcto.CreateAsyncScope();
        Assert.Equal(1, (await nuevo.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct)).Value.Completadas);
        await VerificarAsync(compra, true);
    }

    [Fact]
    public async Task ConsultaFallidaNoEquivaleANoPagoNiDevuelveCupo()
    {
        var compra = await PrepararAsync(); await using var provider = Servicios(new ConsultaFallida()); await using var scope = provider.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct));
        await using var db = postgres.CreateContext();
        Assert.Equal("PROCESANDO", await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal(3, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == compra.MensajeId, Ct));
        await using var accesible = Servicios(new SoloConsulta(Gateway())); await using var siguiente = accesible.CreateAsyncScope();
        await siguiente.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct);
    }

    [Fact]
    public async Task CompraVigenteOConLockSeOmiteSinPersistirMarcadorProvisional()
    {
        var vigente = await PrepararAsync(minutos: -1); var bloqueada = await PrepararAsync();
        await using var lockDb = postgres.CreateContext(); await using var tx = await lockDb.Database.BeginTransactionAsync(Ct);
        await lockDb.Compras.FromSqlInterpolated($"SELECT * FROM compras WHERE id={bloqueada.Id} FOR UPDATE").ToArrayAsync(Ct);
        await using var provider = Servicios(new SoloConsulta(Gateway())); await using var scope = provider.CreateAsyncScope();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), timeout.Token);
        Assert.True(result.IsSuccess); Assert.True(result.Value.Omitidas >= 1);
        await using var db = postgres.CreateContext();
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == bloqueada.MensajeId || m.MensajeId == vigente.MensajeId, Ct));
        await tx.RollbackAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct);
    }

    [Fact]
    public async Task PagoHistoricoAprobadoSinDiarioQuedaParaConciliacion()
    {
        var compra = await PrepararAsync(); await using var provider = Servicios(new SoloConsulta(Gateway()));
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PromotoraDbContext>(); await using var tx = await db.Database.BeginTransactionAsync(Ct);
            var pagos = scope.ServiceProvider.GetRequiredService<IRegistroPagoPort>();
            var inicio = await pagos.IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", Ct);
            Assert.True((await pagos.ResolverEnTransaccionAsync(inicio.Value.Id, new(compra.PublicId, 246.40m, "GTQ", "SIMULADA", "APROBADO", Clock.UtcNow), Ct)).IsSuccess);
            await tx.CommitAsync(Ct);
        }
        await using var nuevo = provider.CreateAsyncScope();
        Assert.True((await nuevo.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct)).Value.Omitidas >= 1);
        await using var check = postgres.CreateContext();
        Assert.Equal("PROCESANDO", await check.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal(3, await check.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
    }

    [Theory]
    [InlineData(0, "FALLIDA", false)]
    [InlineData(1, "FALLIDA", false)]
    [InlineData(0, "COMPLETADA", false)]
    [InlineData(0, "FALLIDA", true)]
    [InlineData(1, "FALLIDA", true)]
    [InlineData(0, "COMPLETADA", true)]
    public async Task ProcesarExpirarCancelarYRecuperarNoDevuelvenCupoDosVeces(int ronda, string esperado, bool procesando)
    {
        var compra = await PrepararAsync(procesando: procesando);
        if (procesando && ronda == 0) await Gateway(esperado == "FALLIDA" ? 1m : 0m).CobrarAsync(new(compra.PublicId, 246.40m, "GTQ"), Ct);
        await using var provider = Servicios(Gateway(esperado == "FALLIDA" ? 1m : 0m));
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task EjecutarAsync(int operacion)
        {
            await salida.Task.WaitAsync(Ct); await using var scope = provider.CreateAsyncScope(); var sp = scope.ServiceProvider;
            if (operacion == 0) await sp.GetRequiredService<IProcesarCompraMensajePort>().ProcesarAsync(new(compra.MensajeId, compra.PublicId), Ct);
            else if (operacion == 1) await sp.GetRequiredService<ICommandHandler<ExpirarComprasCommand, ExpiracionLoteDto>>().HandleAsync(new(), Ct);
            else if (operacion == 2)
                await sp.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
                {
                    await sp.GetRequiredService<ICupoEntradaRepository>().FinalizarYDevolverAsync(compra.Id, EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, Clock.UtcNow, token);
                    return Result<Unit>.Success(Unit.Value);
                }, Ct);
            else await sp.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>().HandleAsync(new(), Ct);
        }
        var tareas = Enumerable.Range(0, 4).Select(EjecutarAsync).ToArray(); salida.SetResult(); await Task.WhenAll(tareas);
        await using var db = postgres.CreateContext();
        string estado = await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct);
        Assert.Contains(estado, new[] { "COMPLETADA", "FALLIDA", "EXPIRADA", "CANCELADA" });
        if (procesando) Assert.Equal(esperado, estado);
        Assert.Equal(estado == "COMPLETADA" ? 3 : 5, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        if (estado == "COMPLETADA") Assert.Equal(246.40m, await db.MovimientosFinancieros.Where(m => m.CompraId == compra.Id).SumAsync(m => m.Monto, Ct));
        else Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
    }

    [Fact]
    public async Task HostPeriodicoRecuperaYDetieneSinRepetirEfectos()
    {
        var compra = await PrepararAsync(); var builder = Host.CreateApplicationBuilder();
        Configurar(builder.Services, new SoloConsulta(Gateway())); builder.Services.AddRecuperacionWorker(new(PollIntervalSeconds: 1));
        var host = builder.Build(); await using var lifetime = (IAsyncDisposable)host; await host.StartAsync(Ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await using var db = postgres.CreateContext();
            while (await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(timeout.Token) != "FALLIDA") await Task.Delay(100, timeout.Token);
        }
        finally { await host.StopAsync(Ct); }
        await VerificarAsync(compra, false);
    }

    private PasarelaSimuladaDurable Gateway(decimal tasa = 0m) => new(postgres.ConnectionString, new(tasa), Clock);
    private void Configurar(IServiceCollection services, IPaymentGateway gateway, bool fallar = false)
    {
        services.AddPromotoraInfrastructure(postgres.ConnectionString); services.AddSingleton(gateway);
        services.AddEmisionBoletos(key, 5); services.AddFinalizacionCompras(); services.AddExpiracionCompras();
        services.AddProcesamientoCompras(); services.AddRecuperacionCompras();
        if (fallar) services.AddScoped<IAuditLogger>(sp => new AuditoriaFallida(new EfAuditLogger(sp.GetRequiredService<PromotoraDbContext>(), Clock)));
    }
    private ServiceProvider Servicios(IPaymentGateway gateway, bool fallar = false)
    {
        var services = new ServiceCollection(); Configurar(services, gateway, fallar);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
    private async Task<Preparada> PrepararAsync(int minutos = -10, bool procesando = true)
    {
        await using var db = postgres.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 2, Ct));
        var row = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            FechaCompra = Clock.UtcNow.AddMinutes(-15),
            ExpiraEn = Clock.UtcNow.AddMinutes(-1),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Estado = "PENDIENTE",
            Moneda = "GTQ",
            Subtotal = 200m,
            ComisionPlataforma = 20m,
            Impuestos = 26.40m,
            Total = 246.40m
        };
        db.Compras.Add(row); await db.SaveChangesAsync(Ct);
        var detalle = new DetalleCompraRow { CompraId = row.Id, TipoEntradaId = datos.TipoId, Cantidad = 2, PrecioUnitario = 100m, Subtotal = 200m };
        db.DetalleCompra.Add(detalle); await db.SaveChangesAsync(Ct);
        long mensaje = await new EfOutboxWriter(db, Clock).EscribirAsync(new("Compra", row.PublicId.ToString("D"), "compra.procesar", JsonSerializer.SerializeToElement(new { publicId = row.PublicId })), Ct);
        if (procesando) await db.Compras.Where(c => c.Id == row.Id && c.Estado == "PENDIENTE").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "PROCESANDO").SetProperty(c => c.ProcesandoDesde, Clock.UtcNow.AddMinutes(minutos)), Ct);
        await tx.CommitAsync(Ct); return new(row.Id, row.PublicId, detalle.Id, datos.TipoId, mensaje);
    }
    private async Task VerificarAsync(Preparada compra, bool aprobada)
    {
        await using var db = postgres.CreateContext();
        Assert.Equal(aprobada ? "COMPLETADA" : "FALLIDA", await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal(aprobada ? 3 : 5, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.Equal(aprobada ? 2 : 0, await db.Boletos.CountAsync(b => b.DetalleCompraId == compra.DetalleId, Ct));
        Assert.Equal(aprobada ? 3 : 0, await db.MovimientosFinancieros.CountAsync(m => m.CompraId == compra.Id, Ct));
        if (aprobada)
        {
            Assert.Equal(246.40m, await db.MovimientosFinancieros.Where(m => m.CompraId == compra.Id).SumAsync(m => m.Monto, Ct));
            Assert.Equal(1, await db.Pagos.CountAsync(p => p.CompraId == compra.Id && p.Estado == "APROBADO", Ct));
            foreach (var boleto in await db.Boletos.Where(b => b.DetalleCompraId == compra.DetalleId).ToArrayAsync(Ct))
                Assert.True(new HmacTicketTokenService(key).Validar(boleto.CodigoQr, out _));
        }
        Assert.Equal(1, await db.MensajesProcesados.CountAsync(m => m.MensajeId == compra.MensajeId && m.Consumidor == "ProcesarCompraConsumer", Ct));
        Assert.Equal(aprobada ? 1 : 0, await db.OutboxMensajes.CountAsync(o => o.AgregadoId == compra.PublicId.ToString("D") && o.Tipo == "compra.completada", Ct));
        Assert.False(await db.CuentasFinancieras.AnyAsync(c => c.Saldo != (db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), Ct));
    }
    private sealed record Preparada(int Id, Guid PublicId, int DetalleId, int TipoId, long MensajeId);
    private sealed class SoloConsulta(IPaymentGateway inner) : IPaymentGateway
    {
        public Task<Result<RespuestaPago>> CobrarAsync(SolicitudPago solicitud, CancellationToken ct) => throw new InvalidOperationException("La recuperación nunca debe cobrar.");
        public Task<RespuestaPago?> ConsultarAsync(Guid referencia, CancellationToken ct) => inner.ConsultarAsync(referencia, ct);
    }
    private sealed class ConsultaFallida : IPaymentGateway
    {
        public Task<Result<RespuestaPago>> CobrarAsync(SolicitudPago solicitud, CancellationToken ct) => throw new InvalidOperationException();
        public Task<RespuestaPago?> ConsultarAsync(Guid referencia, CancellationToken ct) => throw new InvalidOperationException("Proveedor inaccesible en prueba.");
    }
    private sealed class AuditoriaFallida(IAuditLogger inner) : IAuditLogger
    {
        public Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, CancellationToken ct) => WriteAsync(usuario, accion, entidad, id, ip, null, ct);
        public async Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            await inner.WriteAsync(usuario, accion, entidad, id, ip, detalle, ct);
            if (entidad == "movimientos_financieros") throw new InvalidOperationException("Fallo tras ledger de recuperación.");
        }
    }
}
