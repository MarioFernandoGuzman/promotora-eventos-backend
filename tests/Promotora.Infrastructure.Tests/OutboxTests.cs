using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Worker.Messaging;
using RabbitMQ.Client;

namespace Promotora.Infrastructure.Tests;

public sealed class OutboxTests(PostgresFixture postgres, RabbitMqFixture rabbit) : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static IClock Clock => new SystemClock(TimeProvider.System);

    [Fact]
    public async Task NegocioYOutboxConfirmanJuntosOSeReviertenJuntos()
    {
        var agregado = Guid.NewGuid().ToString("N");
        await using var db = postgres.CreateContext();
        var writer = new EfOutboxWriter(db, Clock);
        var uow = new EfUnitOfWork(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.EscribirAsync(Mensaje(agregado), Ct));
        var result = await uow.ExecuteInTransactionAsync(async ct =>
        {
            db.CategoriasEvento.Add(new CategoriaEventoRow { Nombre = agregado });
            await writer.EscribirAsync(Mensaje(agregado), ct);
            return Result<Unit>.Failure(new Error("rollback", "Prueba", ErrorType.BusinessRule));
        }, Ct);
        Assert.True(result.IsFailure);
        await using var verification = postgres.CreateContext();
        Assert.False(await verification.OutboxMensajes.AnyAsync(m => m.AgregadoId == agregado, Ct));
        Assert.False(await verification.CategoriasEvento.AnyAsync(c => c.Nombre == agregado, Ct));
        var id = await CrearAsync(agregado);
        var row = await verification.OutboxMensajes.AsNoTracking().SingleAsync(m => m.Id == id, Ct);
        Assert.False(row.Publicado);
        Assert.Equal(0, row.Intentos);
        Assert.Null(row.FechaPublicado);
        Assert.Equal(DateTimeKind.Utc, row.Fecha.Kind);
        Assert.True(await verification.CategoriasEvento.AnyAsync(c => c.Nombre == agregado, Ct));
        await PublicarPendientesAsync();
        await RecibirAsync(id);
    }

    [Fact]
    public async Task FalloNoMarcaPublicadoYReintentoConservaIdentidad()
    {
        var id = await CrearAsync(Guid.NewGuid().ToString("N"));
        await using (var db = postgres.CreateContext())
        {
            var relay = new EfOutboxRelay(db, new FallarPublisher(), Clock);
            Assert.Equal(new ResultadoRelayOutbox(0, 1), await relay.PublicarLoteAsync(50, Ct));
        }
        await using var verification = postgres.CreateContext();
        var pending = await verification.OutboxMensajes.AsNoTracking().SingleAsync(m => m.Id == id, Ct);
        Assert.False(pending.Publicado);
        Assert.Equal(1, pending.Intentos);
        Assert.Null(pending.FechaPublicado);
        Assert.Equal(new ResultadoRelayOutbox(1, 0), await PublicarPendientesAsync());
        var published = await verification.OutboxMensajes.AsNoTracking().SingleAsync(m => m.Id == id, Ct);
        Assert.True(published.Publicado);
        Assert.Equal(2, published.Intentos);
        Assert.Equal(DateTimeKind.Utc, published.FechaPublicado!.Value.Kind);
        await RecibirAsync(id);
        Assert.Equal(new ResultadoRelayOutbox(0, 0), await PublicarPendientesAsync());
    }

    [Fact]
    public async Task DosRelaysSaltanFilaBloqueadaSinPublicarDosVeces()
    {
        var primero = await CrearAsync(Guid.NewGuid().ToString("N"));
        var segundo = await CrearAsync(Guid.NewGuid().ToString("N"));
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        var bloqueado = new BloquearPublisher(publisher);
        await using var db1 = postgres.CreateContext();
        await using var db2 = postgres.CreateContext();
        var first = new EfOutboxRelay(db1, bloqueado, Clock).PublicarLoteAsync(1, Ct);
        try
        {
            await bloqueado.Tomado.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            Assert.Equal(new ResultadoRelayOutbox(1, 0), await new EfOutboxRelay(db2, publisher, Clock).PublicarLoteAsync(10, Ct));
            await RecibirAsync(segundo);
        }
        finally { bloqueado.Continuar.TrySetResult(); }
        Assert.Equal(new ResultadoRelayOutbox(1, 0), await first);
        await RecibirAsync(primero);
    }

    [Fact]
    public async Task CaidaTrasConfirmacionAntesDeCommitRepiteMismoIdSinPerderMensaje()
    {
        var id = await CrearAsync(Guid.NewGuid().ToString("N"));
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        await using (var db = postgres.CreateContext())
        {
            var relay = new EfOutboxRelay(db, new CancelarTrasPublicar(publisher, shutdown), Clock);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => relay.PublicarLoteAsync(1, shutdown.Token));
        }
        await RecibirAsync(id);
        await using var verification = postgres.CreateContext();
        var pending = await verification.OutboxMensajes.AsNoTracking().SingleAsync(m => m.Id == id, Ct);
        Assert.False(pending.Publicado);
        Assert.Equal(0, pending.Intentos); // La transacción del relay no hizo commit.
        Assert.Null(pending.FechaPublicado);
        await PublicarPendientesAsync();
        await RecibirAsync(id); // Duplicado permitido: el futuro consumidor deduplica este ID estable.
    }

    [Fact]
    public async Task RetornoSinDestinoDelBrokerConservaOutboxPendiente()
    {
        var id = await CrearAsync(Guid.NewGuid().ToString("N"));
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        await publisher.PrepararAsync(Ct);
        await using var connection = await rabbit.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.QueueUnbindAsync(RabbitMqTopology.ComprasQueue, RabbitMqTopology.Exchange, TiposMensaje.ProcesarCompra, cancellationToken: Ct);
        try
        {
            await using var db = postgres.CreateContext();
            Assert.Equal(new ResultadoRelayOutbox(0, 1), await new EfOutboxRelay(db, publisher, Clock).PublicarLoteAsync(10, Ct));
            Assert.False((await db.OutboxMensajes.AsNoTracking().SingleAsync(m => m.Id == id, Ct)).Publicado);
        }
        finally { await RabbitMqTopology.DeclararAsync(channel, Ct); }
        await PublicarPendientesAsync();
        await RecibirAsync(id);
    }

    [Fact]
    public async Task RelayRechazaTransaccionDeNegocioYWriterValidaMensaje()
    {
        await using var db = postgres.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var writer = new EfOutboxWriter(db, Clock);
        await Assert.ThrowsAsync<ArgumentException>(() => writer.EscribirAsync(Mensaje("test") with { Tipo = "desconocido" }, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfOutboxRelay(db, new FallarPublisher(), Clock).PublicarLoteAsync(1, Ct));
        Assert.Throws<InvalidOperationException>(() => new OutboxRelayOptions(0).Validar());
    }

    [Fact]
    public async Task ServicioWorkerPublicaPendienteYCierraConCancelacion()
    {
        var id = await CrearAsync(Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPromotoraInfrastructure(postgres.ConnectionString);
        builder.Services.AddRabbitMqMessaging(rabbit.Options).AddOutboxRelay();
        builder.Services.AddSingleton(new OutboxRelayOptions(5, 1));
        builder.Services.AddHostedService<OutboxRelayService>();
        var host = builder.Build();
        await using var hostLifetime = (IAsyncDisposable)host;
        await host.StartAsync(Ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await using var db = postgres.CreateContext();
            while (!await db.OutboxMensajes.AnyAsync(m => m.Id == id && m.Publicado, timeout.Token))
                await Task.Delay(50, timeout.Token);
            await RecibirAsync(id);
        }
        finally { await host.StopAsync(Ct); }
    }

    private static NuevoMensajeOutbox Mensaje(string agregadoId) => new("Compra", agregadoId, TiposMensaje.ProcesarCompra,
        JsonSerializer.SerializeToElement(new { publicId = agregadoId }));

    private async Task<long> CrearAsync(string agregado)
    {
        await using var db = postgres.CreateContext();
        var result = await new EfUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
        {
            db.CategoriasEvento.Add(new CategoriaEventoRow { Nombre = agregado });
            var id = await new EfOutboxWriter(db, Clock).EscribirAsync(Mensaje(agregado), ct);
            return Result<long>.Success(id);
        }, Ct);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<ResultadoRelayOutbox> PublicarPendientesAsync()
    {
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        await using var db = postgres.CreateContext();
        return await new EfOutboxRelay(db, publisher, Clock).PublicarLoteAsync(50, Ct);
    }

    private async Task RecibirAsync(long id)
    {
        await using var connection = await rabbit.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        var message = await channel.BasicGetAsync(RabbitMqTopology.ComprasQueue, false, Ct);
        Assert.NotNull(message);
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), message.BasicProperties.MessageId);
        Assert.True(message.BasicProperties.Persistent);
        using var json = JsonDocument.Parse(message.Body);
        Assert.True(json.RootElement.TryGetProperty("publicId", out _));
        await channel.BasicAckAsync(message.DeliveryTag, false, Ct);
    }

    private sealed class FallarPublisher : IMessagePublisher
    {
        public Task PrepararAsync(CancellationToken ct) => Task.CompletedTask;
        public Task PublicarAsync(MensajePublicable mensaje, CancellationToken ct) => throw new InvalidOperationException("Fallo de transporte simulado");
    }

    private sealed class BloquearPublisher(IMessagePublisher inner) : IMessagePublisher
    {
        public TaskCompletionSource Tomado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task PrepararAsync(CancellationToken ct) => inner.PrepararAsync(ct);
        public async Task PublicarAsync(MensajePublicable mensaje, CancellationToken ct)
        {
            Tomado.TrySetResult();
            await Continuar.Task.WaitAsync(ct);
            await inner.PublicarAsync(mensaje, ct);
        }
    }

    private sealed class CancelarTrasPublicar(IMessagePublisher inner, CancellationTokenSource shutdown) : IMessagePublisher
    {
        public Task PrepararAsync(CancellationToken ct) => inner.PrepararAsync(ct);
        public async Task PublicarAsync(MensajePublicable mensaje, CancellationToken ct)
        {
            await inner.PublicarAsync(mensaje, ct);
            await shutdown.CancelAsync();
            ct.ThrowIfCancellationRequested();
        }
    }
}
