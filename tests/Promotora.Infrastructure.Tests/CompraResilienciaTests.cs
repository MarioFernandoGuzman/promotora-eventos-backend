using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Messaging;
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
using RabbitMQ.Client;

namespace Promotora.Infrastructure.Tests;

public sealed class CompraResilienciaTests(PostgresFixture postgres, RabbitMqFixture rabbit)
    : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task VeinteDuplicadosConDosConsumidoresConfirmanUnaVentaYPagoSinRepetirEfectos()
    {
        var compra = await PrepararAsync(); var estado = new Control(); var gateway = new PagoObservado();
        await using var uno = Servicios(estado, gateway); await using var dos = Servicios(estado, gateway);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var first = uno.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        var second = dos.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await PublicarAsync(uno, compra, 20);
            await EsperarAsync(async () => Volatile.Read(ref estado.Confirmadas) == 20 && await VaciaAsync());
            await VerificarVentaAsync(compra);
            Assert.Equal(1, Volatile.Read(ref gateway.Cobros));
        }
        finally { await DetenerAsync(stop, first, second); }
    }

    [Fact]
    public async Task ExcepcionTrasLedgerRevierteYRetryCompletaLaVentaUnaSolaVez()
    {
        var compra = await PrepararAsync(); var estado = new Control { Fallos = 1 }; var gateway = new PagoObservado();
        await using var services = Servicios(estado, gateway); using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await PublicarAsync(services, compra);
            await EsperarAsync(async () => Volatile.Read(ref estado.Confirmadas) == 1 && await VaciaAsync());
            Assert.Equal(2, Volatile.Read(ref estado.Entregas));
            Assert.Equal(4, Volatile.Read(ref estado.AuditoriasLedger)); // Una fallida + las tres confirmadas.
            await VerificarVentaAsync(compra);
            Assert.Equal(2, Volatile.Read(ref gateway.Cobros)); // Dos invocaciones, mismo resultado por referencia.
            Assert.Equal(compra.PublicId, (await gateway.ConsultarAsync(compra.PublicId, Ct))!.Referencia);
            Assert.Null(await LeerAsync(RabbitMqTopology.DeadLetterQueue, esperar: false));
        }
        finally { await DetenerAsync(stop, consumer); }
    }

    [Fact]
    public async Task TresFallosTrasLedgerLleganADlqSinPagoLedgerBoletosNiDedupConfirmados()
    {
        var compra = await PrepararAsync(); var estado = new Control { Fallos = int.MaxValue };
        await using var services = Servicios(estado, new()); using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await PublicarAsync(services, compra);
            var dlq = (await LeerAsync(RabbitMqTopology.DeadLetterQueue))!;
            Assert.Equal(compra.MensajeId.ToString(CultureInfo.InvariantCulture), dlq.BasicProperties.MessageId);
            Assert.Equal(3, dlq.BasicProperties.Headers!["x-intentos"]); Assert.True(dlq.BasicProperties.Persistent);
            using var payload = JsonDocument.Parse(dlq.Body); Assert.Single(payload.RootElement.EnumerateObject());
            Assert.Equal(compra.PublicId, payload.RootElement.GetProperty("publicId").GetGuid());
            Assert.Equal(3, Volatile.Read(ref estado.Entregas));
            await EsperarAsync(VaciaAsync); await VerificarRollbackAsync(compra);
        }
        finally { await DetenerAsync(stop, consumer); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorteRealDelBrokerAntesODespuesDelCommitReentregaSinDuplicarVenta(bool despuesCommit)
    {
        var compra = await PrepararAsync();
        var estado = new Control { BloquearEmision = !despuesCommit, BloquearTrasCommit = despuesCommit };
        await using var services = Servicios(estado, new()); using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await PublicarAsync(services, compra);
            await estado.Pausa.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            // Fuerza pérdida de la conexión AMQP durante la emisión o después del commit, antes del ACK.
            await rabbit.InterrumpirConsumidorAsync(RabbitMqTopology.ComprasQueue, Ct);
        }
        finally { await DetenerAsync(stop, consumer, corteEsperado: true); }
        if (despuesCommit) await VerificarVentaAsync(compra); else await VerificarRollbackAsync(compra);
        await using (var connection = await rabbit.ConectarAsync(Ct))
        await using (var channel = await connection.CreateChannelAsync(cancellationToken: Ct))
        {
            BasicGetResult? original = null;
            await EsperarAsync(async () => (original = await channel.BasicGetAsync(RabbitMqTopology.ComprasQueue, false, Ct)) is not null);
            Assert.True(original!.Redelivered); Assert.Equal(compra.MensajeId.ToString(CultureInfo.InvariantCulture), original.BasicProperties.MessageId);
            await channel.BasicNackAsync(original.DeliveryTag, false, requeue: true, Ct);
        }
        var nuevoEstado = new Control(); var nuevaPasarela = new PagoObservado();
        await using var reiniciado = Servicios(nuevoEstado, nuevaPasarela); using var restart = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var siguiente = reiniciado.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(restart.Token);
        try
        {
            await EsperarAsync(async () => Volatile.Read(ref nuevoEstado.Confirmadas) == 1 && await VaciaAsync());
            await VerificarVentaAsync(compra);
            Assert.Equal(despuesCommit ? 0 : 1, Volatile.Read(ref nuevaPasarela.Cobros));
        }
        finally { await DetenerAsync(restart, siguiente); }
    }

    private ServiceProvider Servicios(Control estado, PagoObservado gateway)
    {
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(postgres.ConnectionString);
        services.AddSingleton<IPaymentGateway>(gateway); services.AddEmisionBoletos(_key, 5); services.AddFinalizacionCompras();
        services.AddExpiracionCompras(); services.AddProcesamientoCompras(); services.AddRabbitMqMessaging(rabbit.Options);
        services.AddSingleton(estado);
        services.AddScoped<IAuditLogger>(sp => new AuditoriaControlada(new EfAuditLogger(sp.GetRequiredService<PromotoraDbContext>(), Clock), estado));
        services.AddScoped<ProcesarCompraService>(); services.AddScoped<IProcesarCompraMensajePort, ProcesadorObservado>();
        services.AddCompraConsumer(new(Prefetch: 1, MaxIntentos: 3, RetryBaseDelayMilliseconds: 100));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task PublicarAsync(ServiceProvider services, Preparada compra, int veces = 1)
    {
        var publisher = services.GetRequiredService<IMessagePublisher>();
        var mensaje = new MensajePublicable(compra.MensajeId, TiposMensaje.ProcesarCompra, JsonSerializer.SerializeToUtf8Bytes(new { publicId = compra.PublicId }));
        for (int i = 0; i < veces; i++) await publisher.PublicarAsync(mensaje, Ct);
    }

    private async Task<Preparada> PrepararAsync()
    {
        await using var db = postgres.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.True(await new EfCupoEntradaRepository(db).ReservarAsync(datos.EventoId, datos.TipoId, 2, Ct));
        var row = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            FechaCompra = Clock.UtcNow,
            ExpiraEn = Clock.UtcNow.AddMinutes(15),
            IdempotencyKey = Guid.NewGuid().ToString(),
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
        long mensajeId = await new EfOutboxWriter(db, Clock).EscribirAsync(new("Compra", row.PublicId.ToString("D"), TiposMensaje.ProcesarCompra,
            JsonSerializer.SerializeToElement(new { publicId = row.PublicId })), Ct);
        await tx.CommitAsync(Ct); return new(row.Id, row.PublicId, detalle.Id, datos.TipoId, mensajeId);
    }

    private async Task VerificarVentaAsync(Preparada compra)
    {
        await using var db = postgres.CreateContext();
        Assert.Equal("COMPLETADA", await db.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.Equal(1, await db.Pagos.CountAsync(p => p.CompraId == compra.Id && p.Estado == "APROBADO" && p.Monto == 246.40m, Ct));
        var ledger = await db.MovimientosFinancieros.Where(m => m.CompraId == compra.Id).ToArrayAsync(Ct);
        Assert.Equal(3, ledger.Length); Assert.Equal(246.40m, ledger.Sum(m => m.Monto));
        var boletos = await db.Boletos.Where(b => b.DetalleCompraId == compra.DetalleId).ToArrayAsync(Ct);
        Assert.Equal(2, boletos.Length); Assert.All(boletos, b => Assert.True(new HmacTicketTokenService(_key).Validar(b.CodigoQr, out _)));
        Assert.Equal(1, await db.MensajesProcesados.CountAsync(m => m.MensajeId == compra.MensajeId && m.Consumidor == "ProcesarCompraConsumer", Ct));
        Assert.Equal(1, await db.OutboxMensajes.CountAsync(o => o.AgregadoId == compra.PublicId.ToString("D") && o.Tipo == "compra.completada", Ct));
        Assert.Equal(3, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await db.CuentasFinancieras.AnyAsync(c => c.Saldo != (db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), Ct));
        var ids = ledger.Select(m => m.Id.ToString()).ToArray();
        Assert.Equal(3, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "movimientos_financieros" && ids.Contains(a.EntidadId!), Ct));
    }

    private async Task VerificarRollbackAsync(Preparada compra)
    {
        await using var db = postgres.CreateContext();
        var row = await db.Compras.AsNoTracking().SingleAsync(c => c.Id == compra.Id, Ct);
        Assert.Equal("PENDIENTE", row.Estado); Assert.Null(row.ProcesandoDesde);
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == compra.Id, Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, Ct));
        Assert.False(await db.Boletos.AnyAsync(b => b.DetalleCompraId == compra.DetalleId, Ct));
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == compra.MensajeId, Ct));
        Assert.False(await db.OutboxMensajes.AnyAsync(o => o.AgregadoId == compra.PublicId.ToString("D") && o.Tipo == "compra.completada", Ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString(), Ct));
        Assert.Equal(3, await db.TiposEntrada.Where(t => t.Id == compra.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await db.CuentasFinancieras.AnyAsync(c => c.Saldo != (db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), Ct));
    }

    private async Task<BasicGetResult?> LeerAsync(string queue, bool esperar = true)
    {
        await using var connection = await rabbit.ConectarAsync(Ct); await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        BasicGetResult? result = null;
        if (esperar) await EsperarAsync(async () => (result = await channel.BasicGetAsync(queue, false, Ct)) is not null);
        else result = await channel.BasicGetAsync(queue, false, Ct);
        if (result is not null) await channel.BasicAckAsync(result.DeliveryTag, false, Ct);
        return result;
    }
    private async Task<bool> VaciaAsync() => await rabbit.EstadoColaAsync(RabbitMqTopology.ComprasQueue, Ct) == (0, 0);
    private static async Task EsperarAsync(Func<Task<bool>> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await predicate()) await Task.Delay(100, timeout.Token);
    }
    private static Task DetenerAsync(CancellationTokenSource stop, Task consumer, bool corteEsperado = false) => DetenerAsync(stop, [consumer], corteEsperado);
    private static Task DetenerAsync(CancellationTokenSource stop, Task one, Task two) => DetenerAsync(stop, [one, two], false);
    private static async Task DetenerAsync(CancellationTokenSource stop, Task[] consumers, bool corteEsperado)
    {
        await stop.CancelAsync();
        foreach (var consumer in consumers)
        {
            try { await consumer.WaitAsync(TimeSpan.FromSeconds(15), Ct); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (InvalidOperationException) when (corteEsperado) { }
        }
    }
    private sealed record Preparada(int Id, Guid PublicId, int DetalleId, int TipoId, long MensajeId);
    private sealed class Control
    {
        public int Entregas;
        public int Confirmadas;
        public int AuditoriasLedger;
        public int Fallos;
        public bool BloquearEmision;
        public bool BloquearTrasCommit;
        public TaskCompletionSource Pausa { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class ProcesadorObservado(ProcesarCompraService inner, Control estado) : IProcesarCompraMensajePort
    {
        public async Task<Result<Unit>> ProcesarAsync(MensajeCompraDto mensaje, CancellationToken ct)
        {
            Interlocked.Increment(ref estado.Entregas);
            var result = await inner.ProcesarAsync(mensaje, ct);
            if (result.IsSuccess) Interlocked.Increment(ref estado.Confirmadas);
            if (result.IsSuccess && estado.BloquearTrasCommit)
            {
                estado.Pausa.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            return result;
        }
    }
    private sealed class AuditoriaControlada(IAuditLogger inner, Control estado) : IAuditLogger
    {
        public Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, CancellationToken ct) => WriteAsync(usuario, accion, entidad, id, ip, null, ct);
        public async Task WriteAsync(int? usuario, string accion, string entidad, string? id, string? ip, DetalleAuditoria? detalle, CancellationToken ct)
        {
            await inner.WriteAsync(usuario, accion, entidad, id, ip, detalle, ct);
            if (entidad == "movimientos_financieros" && Interlocked.Increment(ref estado.AuditoriasLedger) <= estado.Fallos)
                throw new InvalidOperationException("Fallo de prueba después de insertar el ledger.");
            if (entidad == "boletos" && estado.BloquearEmision)
            {
                estado.Pausa.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
        }
    }
    private sealed class PagoObservado : IPaymentGateway
    {
        private readonly FakePaymentGateway inner = new(new(), Clock);
        public int Cobros;
        public Task<Result<RespuestaPago>> CobrarAsync(SolicitudPago solicitud, CancellationToken ct)
        {
            Interlocked.Increment(ref Cobros); return inner.CobrarAsync(solicitud, ct);
        }
        public Task<RespuestaPago?> ConsultarAsync(Guid referencia, CancellationToken ct) => inner.ConsultarAsync(referencia, ct);
    }
}
