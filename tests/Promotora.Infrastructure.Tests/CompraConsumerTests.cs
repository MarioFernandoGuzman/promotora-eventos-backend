using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;
using RabbitMQ.Client;

namespace Promotora.Infrastructure.Tests;

public sealed class CompraConsumerTests(PostgresFixture postgres, RabbitMqFixture rabbit) : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MensajeDuplicadoHaceUnSoloEfectoYUnRegistroTransaccional()
    {
        var mensaje = await MensajeAsync();
        var estado = new EstadoPrueba();
        await using var services = Servicios(estado);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await publisher.PublicarAsync(mensaje, Ct);
            await publisher.PublicarAsync(mensaje, Ct);
            await EsperarEfectoConfirmadoAsync(mensaje.Id);
            await EsperarAsync(async () => Volatile.Read(ref estado.Invocaciones) >= 2 && await VaciaAsync(), Ct);
            await using var db = postgres.CreateContext();
            Assert.Equal(1, await db.CategoriasEvento.CountAsync(c => c.Nombre == $"efecto_{mensaje.Id}", Ct));
            Assert.Equal(1, await db.MensajesProcesados.CountAsync(m => m.MensajeId == mensaje.Id && m.Consumidor == "ProcesarCompraConsumer", Ct));
        }
        finally { await DetenerAsync(stop, consumer); }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(99, true)]
    public async Task ExcepcionReintentaConBackoffYLlevaADlqAlTercerIntento(int fallos, bool dlq)
    {
        var mensaje = await MensajeAsync();
        var estado = new EstadoPrueba { Fallos = fallos, PausarAntesDeCommit = !dlq };
        await using var services = Servicios(estado);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await publisher.PublicarAsync(mensaje, Ct);
            if (dlq)
            {
                var resultado = await RecibirAsync(RabbitMqTopology.DeadLetterQueue);
                Assert.Equal(mensaje.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), resultado.BasicProperties.MessageId);
                Assert.Equal(3, resultado.BasicProperties.Headers!["x-intentos"]);
                Assert.True(resultado.BasicProperties.Persistent);
                Assert.Null(resultado.BasicProperties.Expiration);
                Assert.Equal(3, Volatile.Read(ref estado.Invocaciones));
            }
            else
            {
                // Un segundo intento iniciado todavía puede tener su transacción sin confirmar.
                await estado.Tomado.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
                Assert.Equal(2, Volatile.Read(ref estado.Invocaciones));
                await using var antesDelCommit = postgres.CreateContext();
                Assert.False(await antesDelCommit.MensajesProcesados.AnyAsync(m => m.MensajeId == mensaje.Id, Ct));
                Assert.False(await antesDelCommit.CategoriasEvento.AnyAsync(c => c.Nombre == $"efecto_{mensaje.Id}", Ct));
                estado.Continuar.TrySetResult();
                await EsperarEfectoConfirmadoAsync(mensaje.Id);
            }
            await EsperarAsync(async () => Volatile.Read(ref estado.Invocaciones) >= (dlq ? 3 : 2) && await VaciaAsync(), Ct);
            await using var db = postgres.CreateContext();
            Assert.Equal(dlq ? 0 : 1, await db.MensajesProcesados.CountAsync(m => m.MensajeId == mensaje.Id, Ct));
            Assert.Equal(dlq ? 0 : 1, await db.CategoriasEvento.CountAsync(c => c.Nombre == $"efecto_{mensaje.Id}", Ct));
        }
        finally { await DetenerAsync(stop, consumer); }
    }

    [Fact]
    public async Task MensajeMalformadoSeTrasladaADlqSinInvocarNegocio()
    {
        var mensaje = await MensajeAsync();
        var estado = new EstadoPrueba();
        await using var services = Servicios(estado);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await publisher.PublicarAsync(mensaje with { Payload = Encoding.UTF8.GetBytes("{\"publicId\":\"invalido\"}") }, Ct);
            var dlq = await RecibirAsync(RabbitMqTopology.DeadLetterQueue);
            Assert.Equal(mensaje.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), dlq.BasicProperties.MessageId);
            await EsperarAsync(VaciaAsync, Ct);
            Assert.Equal(0, Volatile.Read(ref estado.Invocaciones));
        }
        finally { await DetenerAsync(stop, consumer); }
    }

    [Fact]
    public async Task CancelarDuranteEfectoRevierteDedupYBrokerReentregaElOriginal()
    {
        var mensaje = await MensajeAsync();
        var estado = new EstadoPrueba { Bloquear = true };
        await using var services = Servicios(estado);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        await publisher.PublicarAsync(mensaje, Ct);
        await estado.Tomado.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        await DetenerAsync(stop, consumer);
        await using var db = postgres.CreateContext();
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == mensaje.Id, Ct));
        Assert.False(await db.CategoriasEvento.AnyAsync(c => c.Nombre == $"efecto_{mensaje.Id}", Ct));
        var original = await RecibirAsync(RabbitMqTopology.ComprasQueue);
        Assert.True(original.Redelivered);
        Assert.Equal(mensaje.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), original.BasicProperties.MessageId);
    }

    [Fact]
    public async Task TomaCondicionalYDedupCompartenRollbackYSoloUnGanadorConcurrente()
    {
        await using var setup = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(setup, 2, Ct);
        var compra = new CompraRow { ClienteId = datos.ClienteId, EventoId = datos.EventoId, ExpiraEn = DateTime.UtcNow.AddMinutes(15), IdempotencyKey = Guid.NewGuid().ToString("N") };
        setup.Compras.Add(compra); await setup.SaveChangesAsync(Ct);
        var mensaje = await MensajeAsync();
        await using (var services = Servicios(new EstadoPrueba()))
        await using (var scope = services.CreateAsyncScope())
        {
            var toma = scope.ServiceProvider.GetRequiredService<ICompraTomaRepository>();
            var dedup = scope.ServiceProvider.GetRequiredService<IMensajeProcesadoRepository>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => toma.TomarAsync(compra.PublicId, Ct));
            var result = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                Assert.True(await dedup.RegistrarAsync(mensaje.Id, "ProcesarCompraConsumer", ct));
                Assert.NotNull(await toma.TomarAsync(compra.PublicId, ct));
                return Result<Unit>.Failure(new Error("prueba", "Revertir", ErrorType.BusinessRule));
            }, Ct);
            Assert.True(result.IsFailure);
        }
        await using var verify = postgres.CreateContext();
        Assert.False(await verify.MensajesProcesados.AnyAsync(m => m.MensajeId == mensaje.Id, Ct));
        Assert.Equal("PENDIENTE", await verify.Compras.Where(c => c.Id == compra.Id).Select(c => c.Estado).SingleAsync(Ct));
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var services = Servicios(new EstadoPrueba());
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                bool nuevo = await scope.ServiceProvider.GetRequiredService<IMensajeProcesadoRepository>().RegistrarAsync(mensaje.Id, "ProcesarCompraConsumer", ct);
                var tomada = nuevo ? await scope.ServiceProvider.GetRequiredService<ICompraTomaRepository>().TomarAsync(compra.PublicId, ct) : null;
                return Result<bool>.Success(tomada is not null);
            }, Ct);
        }));
        Assert.Equal(1, resultados.Count(r => r.Value));
        var fila = await verify.Compras.AsNoTracking().SingleAsync(c => c.Id == compra.Id, Ct);
        Assert.Equal("PROCESANDO", fila.Estado); Assert.NotNull(fila.ProcesandoDesde);
        Assert.Equal(DateTimeKind.Utc, fila.ProcesandoDesde.Value.Kind);
        Assert.Equal(1, await verify.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString() && a.Accion == "UPDATE", Ct));
    }

    [Fact]
    public async Task FalloDeTrasladoADlqNoHaceAckYReentregaOriginal()
    {
        var mensaje = await MensajeAsync();
        await using var services = Servicios(new EstadoPrueba { Fallos = 99 }, maxIntentos: 1);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        await publisher.PrepararAsync(Ct);
        await using var connection = await rabbit.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumer = services.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
        await EsperarAsync(async () => (await channel.QueueDeclarePassiveAsync(RabbitMqTopology.ComprasQueue, Ct)).ConsumerCount == 1, Ct);
        await channel.QueueUnbindAsync(RabbitMqTopology.DeadLetterQueue, RabbitMqTopology.DeadLetterExchange, TiposMensaje.ProcesarCompra, cancellationToken: Ct);
        try
        {
            await publisher.PublicarAsync(mensaje, Ct);
            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.WaitAsync(TimeSpan.FromSeconds(15), Ct));
        }
        finally
        {
            await stop.CancelAsync();
            await RabbitMqTopology.DeclararAsync(channel, Ct);
        }
        var original = await RecibirAsync(RabbitMqTopology.ComprasQueue);
        Assert.True(original.Redelivered);
        Assert.Equal(mensaje.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), original.BasicProperties.MessageId);
        await using var db = postgres.CreateContext();
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == mensaje.Id, Ct));
    }

    [Fact]
    public void ActivacionSinIntegradorFinancieroFallaCerradaYBackoffEstaAcotado()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddCompraConsumer(new()));
        var options = new CompraConsumerOptions();
        Assert.Equal(5000, options.Retraso(1)); Assert.Equal(10000, options.Retraso(2));
        Assert.Equal(300000, options.Retraso(10));
    }

    private ServiceProvider Servicios(EstadoPrueba estado, int maxIntentos = 3)
    {
        var services = new ServiceCollection();
        services.AddPromotoraInfrastructure(postgres.ConnectionString).AddCompraConsumerComponents();
        services.AddSingleton(rabbit.Options);
        services.AddSingleton(estado);
        services.AddScoped<IProcesarCompraMensajePort, ProcesadorSoloDePrueba>();
        services.AddCompraConsumer(new CompraConsumerOptions(Prefetch: 1, MaxIntentos: maxIntentos, RetryBaseDelayMilliseconds: 50));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private async Task<MensajePublicable> MensajeAsync()
    {
        await using var db = postgres.CreateContext();
        var publicId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToElement(new { publicId });
        var result = await new EfUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
            Result<long>.Success(await new EfOutboxWriter(db, new SystemClock(TimeProvider.System)).EscribirAsync(new("Compra", publicId.ToString("D"), TiposMensaje.ProcesarCompra, payload), ct)), Ct);
        return new(result.Value, TiposMensaje.ProcesarCompra, JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    private async Task<bool> VaciaAsync()
    {
        var (ready, pending) = await rabbit.EstadoColaAsync(RabbitMqTopology.ComprasQueue, Ct);
        var (retryReady, retryPending) = await rabbit.EstadoColaAsync(RabbitMqTopology.RetryQueue, Ct);
        return ready == 0 && pending == 0 && retryReady == 0 && retryPending == 0;
    }

    private Task EsperarEfectoConfirmadoAsync(long mensajeId) => EsperarAsync(async () =>
    {
        // Las estadísticas del broker no prueban que PostgreSQL haya confirmado el efecto.
        await using var db = postgres.CreateContext();
        return await db.MensajesProcesados.AnyAsync(m => m.MensajeId == mensajeId && m.Consumidor == "ProcesarCompraConsumer", Ct)
            && await db.CategoriasEvento.AnyAsync(c => c.Nombre == $"efecto_{mensajeId}", Ct);
    }, Ct);

    private async Task<BasicGetResult> RecibirAsync(string queue)
    {
        await using var connection = await rabbit.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        BasicGetResult? result = null;
        await EsperarAsync(async () => (result = await channel.BasicGetAsync(queue, false, Ct)) is not null, Ct);
        await channel.BasicAckAsync(result!.DeliveryTag, false, Ct);
        return result;
    }

    private static async Task EsperarAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }

    private static async Task DetenerAsync(CancellationTokenSource stop, Task consumer)
    {
        await stop.CancelAsync();
        try { await consumer.WaitAsync(TimeSpan.FromSeconds(15), Ct); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    private sealed class EstadoPrueba
    {
        public int Invocaciones;
        public int Fallos;
        public bool Bloquear;
        public bool PausarAntesDeCommit;
        public TaskCompletionSource Tomado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ProcesadorSoloDePrueba(PromotoraDbContext db, IUnitOfWork uow, IMensajeProcesadoRepository dedup, EstadoPrueba estado) : IProcesarCompraMensajePort
    {
        public Task<Result<Unit>> ProcesarAsync(MensajeCompraDto mensaje, CancellationToken ct) => uow.ExecuteInTransactionAsync(async token =>
        {
            int invocacion = Interlocked.Increment(ref estado.Invocaciones);
            if (!await dedup.RegistrarAsync(mensaje.MensajeId, "ProcesarCompraConsumer", token)) return Result<Unit>.Success(Unit.Value);
            db.CategoriasEvento.Add(new CategoriaEventoRow { Nombre = $"efecto_{mensaje.MensajeId}" });
            await db.SaveChangesAsync(token);
            if (estado.Bloquear)
            {
                estado.Tomado.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            if (invocacion <= estado.Fallos) throw new InvalidOperationException("Fallo transitorio de prueba");
            if (estado.PausarAntesDeCommit)
            {
                estado.Tomado.TrySetResult();
                await estado.Continuar.Task.WaitAsync(token);
            }
            return Result<Unit>.Success(Unit.Value);
        }, ct);
    }
}
