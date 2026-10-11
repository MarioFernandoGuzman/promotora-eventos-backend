using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Identity;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Tests;
using Promotora.Infrastructure.Ticketing;

namespace Promotora.Api.Tests;

public sealed class CompraCargaApiTests(PostgresFixture postgres, RabbitMqFixture rabbit, ITestOutputHelper output)
    : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task CargaConcurrenteCompletaSoloElCupoConPagoLedgerYBoletos(int solicitudes)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(4)); var ct = timeout.Token;
        await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 100, ct);
        // Todas las solicitudes HTTP están en vuelo; el pool limita únicamente las conexiones físicas de esta prueba.
        string connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        { MaxPoolSize = 32, Timeout = 120, CommandTimeout = 120 }.ConnectionString;
        await using var factory = new IdentityApiFactory(connection);
        var clientes = Enumerable.Range(0, 8).Select(_ => factory.CreateClient()).ToArray();
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(connection);
        services.AddPagoSimulado(new()); services.AddEmisionBoletos(factory.QrKey, 5);
        services.AddFinalizacionCompras(); services.AddExpiracionCompras(); services.AddProcesamientoCompras();
        services.AddRabbitMqMessaging(rabbit.Options); services.AddOutboxRelay(); services.AddCompraConsumer(new(Prefetch: 8));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task consumer = Task.CompletedTask, relay = Task.CompletedTask;
        try
        {
            foreach (var client in clientes) client.Timeout = TimeSpan.FromMinutes(3);
            var support = new TicketingApiTestSupport(postgres);
            await Task.WhenAll(clientes.Select(client => support.LoginAsync(client, RolUsuario.CLIENTE, ct)));
            consumer = provider.GetRequiredService<ICompraMessageConsumer>().EjecutarAsync(stop.Token);
            relay = PublicarAsync(provider, stop.Token);
            var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tareas = Enumerable.Range(0, solicitudes).Select(async i =>
            {
                await salida.Task.WaitAsync(ct); var reloj = Stopwatch.StartNew();
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras")
                { Content = JsonContent.Create(new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1) } }) };
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                using var response = await clientes[i % clientes.Length].SendAsync(request, ct);
                var compra = response.StatusCode == HttpStatusCode.Accepted ? await response.Content.ReadFromJsonAsync<CompraCreadaDto>(ct) : null;
                return (response.StatusCode, Compra: compra, Ms: reloj.Elapsed.TotalMilliseconds);
            }).ToArray();
            var total = Stopwatch.StartNew(); salida.SetResult();
            var resultados = await Task.WhenAll(tareas); double reservaMs = total.Elapsed.TotalMilliseconds;
            Assert.Equal(100, resultados.Count(r => r.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(solicitudes - 100, resultados.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(100, resultados.Where(r => r.Compra is not null).Select(r => r.Compra!.PublicId).Distinct().Count());
            while (await db.Compras.CountAsync(c => c.EventoId == datos.EventoId && c.Estado == "COMPLETADA", ct) != 100)
            {
                if (consumer.IsCompleted) await consumer;
                if (relay.IsCompleted) await relay;
                await Task.Delay(100, ct);
            }
            while (await rabbit.EstadoColaAsync(RabbitMqTopology.ComprasQueue, ct) != (0, 0)) await Task.Delay(100, ct);
            var compras = await db.Compras.AsNoTracking().Where(c => c.EventoId == datos.EventoId).ToArrayAsync(ct);
            int[] ids = compras.Select(c => c.Id).ToArray();
            Assert.Equal(100, compras.Length); Assert.All(compras, c => Assert.Equal("COMPLETADA", c.Estado));
            Assert.Equal(0, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
            Assert.Equal(100, await db.Pagos.CountAsync(p => ids.Contains(p.CompraId) && p.Estado == "APROBADO", ct));
            Assert.Equal(100, await db.Boletos.CountAsync(b => db.DetalleCompra.Any(d => d.Id == b.DetalleCompraId && ids.Contains(d.CompraId)), ct));
            var movimientos = await db.MovimientosFinancieros.AsNoTracking().Where(m => m.CompraId != null && ids.Contains(m.CompraId.Value)).ToArrayAsync(ct);
            Assert.Equal(300, movimientos.Length);
            Assert.Equal(12320m, movimientos.Sum(m => m.Monto));
            foreach (var compra in compras)
            {
                Assert.Equal(123.20m, compra.Total);
                Assert.Equal(compra.Total, movimientos.Where(m => m.CompraId == compra.Id).Sum(m => m.Monto));
                Assert.Equal(3, movimientos.Count(m => m.CompraId == compra.Id));
            }
            foreach (var cuenta in await db.CuentasFinancieras.AsNoTracking().ToArrayAsync(ct))
                Assert.Equal(cuenta.Saldo, await db.MovimientosFinancieros.Where(m => m.CuentaId == cuenta.Id).SumAsync(m => m.Monto, ct));
            string[] publicIds = compras.Select(c => c.PublicId.ToString("D")).ToArray();
            Assert.Equal(100, await db.OutboxMensajes.CountAsync(m => publicIds.Contains(m.AgregadoId) && m.Tipo == "compra.completada", ct));
            var outboxIds = await db.OutboxMensajes.Where(m => publicIds.Contains(m.AgregadoId) && m.Tipo == "compra.procesar").Select(m => m.Id).ToArrayAsync(ct);
            Assert.Equal(100, await db.MensajesProcesados.CountAsync(m => outboxIds.Contains(m.MensajeId), ct));
            Assert.Equal((0, 0), await rabbit.EstadoColaAsync(RabbitMqTopology.RetryQueue, ct));
            Assert.Equal((0, 0), await rabbit.EstadoColaAsync(RabbitMqTopology.DeadLetterQueue, ct));
            var tiempos = resultados.Select(r => r.Ms).Order().ToArray();
            double Percentil(double p) => tiempos[(int)Math.Ceiling(tiempos.Length * p) - 1];
            output.WriteLine($"CARGA solicitudes={solicitudes}; aceptadas=100; conflictos={solicitudes - 100}; reserva_ms={reservaMs:F0}; flujo_ms={total.Elapsed.TotalMilliseconds:F0}; p50_ms={Percentil(.50):F0}; p95_ms={Percentil(.95):F0}; p99_ms={Percentil(.99):F0}; pagos=100; boletos=100; movimientos=300; saldo=12320.00");
        }
        finally
        {
            await stop.CancelAsync();
            try { await Task.WhenAll(consumer, relay).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            foreach (var cliente in clientes) cliente.Dispose();
        }
    }

    private static async Task PublicarAsync(ServiceProvider provider, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IOutboxRelay>().PublicarLoteAsync(100, ct);
            await Task.Delay(100, ct);
        }
    }
}
