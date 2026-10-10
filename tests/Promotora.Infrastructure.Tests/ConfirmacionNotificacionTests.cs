using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Notifications;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Notifications;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class ConfirmacionNotificacionTests(PostgresFixture postgres, RabbitMqFixture rabbit, MailHogFixture mail)
    : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>, IClassFixture<MailHogFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RabbitMqEntregaCorreoConBoletosYDeduplicaRepeticion()
    {
        var compra = await CompraAsync("COMPLETADA", true);
        await using var db = postgres.CreateContext();
        long id = await OutboxAsync(db, compra.PublicId);
        var services = new ServiceCollection();
        services.AddPromotoraInfrastructure(postgres.ConnectionString);
        services.AddSingleton(rabbit.Options);
        services.AddEmisionBoletos(null);
        services.AddConfirmacionNotificaciones(mail.Options, new(3, 1));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var task = provider.GetRequiredService<INotificacionConsumer>().EjecutarAsync(stop.Token);
        try
        {
            await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
            var mensaje = new MensajePublicable(id, TiposMensaje.CompraCompletada, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { publicId = compra.PublicId })));
            await publisher.PublicarAsync(mensaje, Ct); await publisher.PublicarAsync(mensaje, Ct);
            await EsperarAsync(async () => await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct)
                && await rabbit.EstadoColaAsync("notificaciones.compra", Ct) == (0, 0));
            Assert.Equal(1, await db.Notificaciones.CountAsync(n => n.UsuarioId == compra.UsuarioId && n.Tipo == TiposMensaje.CompraCompletada && n.Estado == "ENVIADO", Ct));
            using var recibidos = await mail.MensajesAsync(Ct);
            var items = recibidos.RootElement.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("Content").GetProperty("Headers")
                .GetProperty("Message-ID")[0].GetString() == $"<compra-{id}@promotora.invalid>").ToArray();
            Assert.Single(items);
            var body = Encoding.UTF8.GetString(Convert.FromBase64String(items[0].GetProperty("Content").GetProperty("Body").GetString()!));
            Assert.Contains(compra.PublicId.ToString(), body);
            Assert.Contains(compra.Qr, body);
            Assert.False(await db.Notificaciones.AnyAsync(n => n.UsuarioId == compra.UsuarioId && n.Mensaje!.Contains(compra.Qr), Ct));
        }
        finally
        {
            stop.Cancel();
            try { await task.WaitAsync(TimeSpan.FromSeconds(15), Ct); } catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData("PENDIENTE", true)]
    [InlineData("COMPLETADA", false)]
    public async Task NoEnviaSiNoEstaCompletadaOSiFaltanBoletos(string estado, bool boletos)
    {
        var compra = await CompraAsync(estado, boletos);
        await using var db = postgres.CreateContext(); var sender = new CorreoPrueba();
        long id = await OutboxAsync(db, compra.PublicId);
        await Handler(db, sender).ProcesarAsync(new(id, compra.PublicId, true), Ct);
        Assert.Equal(0, sender.Intentos);
        Assert.True(await db.Notificaciones.AnyAsync(n => n.Estado == "FALLIDO" && n.UsuarioId == null, Ct));
        Assert.True(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct));
    }

    [Theory]
    [InlineData(1, "ENVIADO", 2)]
    [InlineData(99, "FALLIDO", 3)]
    public async Task SmtpReintentaYRegistraResultadoSinCuerpo(int fallos, string estado, int intentos)
    {
        var compra = await CompraAsync("COMPLETADA", true);
        await using var db = postgres.CreateContext(); var sender = new CorreoPrueba { Fallos = fallos };
        long id = await OutboxAsync(db, compra.PublicId);
        await Handler(db, sender).ProcesarAsync(new(id, compra.PublicId, true), Ct);
        await Handler(db, sender).ProcesarAsync(new(id, compra.PublicId, true), Ct);
        Assert.Equal(intentos, sender.Intentos);
        var n = await db.Notificaciones.SingleAsync(n => n.UsuarioId == compra.UsuarioId, Ct);
        Assert.Equal(estado, n.Estado); Assert.Equal(estado == "ENVIADO", n.FechaEnvio.HasValue);
        Assert.DoesNotContain(compra.Qr, n.Mensaje!);
    }

    [Fact]
    public async Task VeinteEntregasConcurrentesEnvianUnCorreo()
    {
        var compra = await CompraAsync("COMPLETADA", true); var sender = new CorreoPrueba();
        await using var db = postgres.CreateContext(); long id = await OutboxAsync(db, compra.PublicId);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var contexto = postgres.CreateContext();
            await Handler(contexto, sender).ProcesarAsync(new(id, compra.PublicId, true), Ct);
        }));
        Assert.Equal(1, sender.Intentos);
        Assert.Equal(1, await db.Notificaciones.CountAsync(n => n.UsuarioId == compra.UsuarioId, Ct));
    }

    [Fact]
    public async Task CompraConImporteSinLedgerNoGeneraConfirmacion()
    {
        var compra = await CompraAsync("COMPLETADA", true);
        await using var db = postgres.CreateContext();
        await db.Compras.Where(c => c.PublicId == compra.PublicId).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Subtotal, 100m).SetProperty(c => c.Total, 100m), Ct);
        long id = await OutboxAsync(db, compra.PublicId); var sender = new CorreoPrueba();
        await Handler(db, sender).ProcesarAsync(new(id, compra.PublicId, true), Ct);
        Assert.Equal(0, sender.Intentos);
    }

    [Fact]
    public async Task AdaptadorSmtpOcultaDetallesDelProveedorYRespetaCancelacion()
    {
        var smtp = new SmtpEmailSender(new("127.0.0.1", 1, "promotora@example.invalid", TimeoutSeconds: 1));
        var correo = new CorreoDto("cliente@example.invalid", "Prueba", "Texto", "test@promotora.invalid");
        await Assert.ThrowsAsync<CorreoEntregaException>(() => smtp.EnviarAsync(correo, Ct));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => smtp.EnviarAsync(correo, stop.Token));
        Assert.Throws<ArgumentException>(() => new SmtpOptions("localhost", 1025, "from@example.invalid", Username: "u", Password: Guid.NewGuid().ToString()).Validar());
    }

    [Fact]
    public async Task CancelacionDuranteSmtpRevierteDedupYNotificacion()
    {
        var compra = await CompraAsync("COMPLETADA", true);
        await using var db = postgres.CreateContext(); long id = await OutboxAsync(db, compra.PublicId);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var sender = new CorreoPrueba { Cancelar = stop };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler(db, sender).ProcesarAsync(new(id, compra.PublicId, true), stop.Token));
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct));
        Assert.False(await db.Notificaciones.AnyAsync(n => n.UsuarioId == compra.UsuarioId, Ct));
    }

    [Fact]
    public async Task MensajeInvalidoSeRegistraFallidoSinCorreo()
    {
        await using var db = postgres.CreateContext(); long id = await OutboxAsync(db, Guid.NewGuid()); var sender = new CorreoPrueba();
        await Handler(db, sender).ProcesarAsync(new(id, null, false), Ct);
        Assert.Equal(0, sender.Intentos);
        Assert.True(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct));
    }

    private EnviarConfirmacionHandler Handler(PromotoraDbContext db, IEmailSender sender)
    {
        var clock = new SystemClock(TimeProvider.System);
        return new(new EfNotificacionRepository(db, new EfEmisionBoletosRepository(db, new()), clock),
            new EfMensajeProcesadoRepository(db, clock), new EfUnitOfWork(db), sender, new(3, 1));
    }

    private async Task<(Guid PublicId, int UsuarioId, string Qr)> CompraAsync(string estado, bool boletos)
    {
        await using var db = postgres.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        // Compra gratuita existente: cero movimientos, prueba de notificación; no finalizador de F7.
        var compra = new CompraRow
        {
            PublicId = Guid.NewGuid(),
            ClienteId = datos.ClienteId,
            EventoId = datos.EventoId,
            Estado = estado,
            ExpiraEn = DateTime.UtcNow.AddMinutes(15),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Moneda = "GTQ"
        };
        db.Compras.Add(compra); await db.SaveChangesAsync(Ct);
        var detalle = new DetalleCompraRow { CompraId = compra.Id, TipoEntradaId = datos.TipoId, Cantidad = 1 };
        db.DetalleCompra.Add(detalle); await db.SaveChangesAsync(Ct);
        var boletoPublicId = Guid.NewGuid();
        string qr = new HmacTicketTokenService(RandomNumberGenerator.GetBytes(32)).Firmar(boletoPublicId).Value;
        if (boletos) { db.Boletos.Add(new BoletoRow { PublicId = boletoPublicId, DetalleCompraId = detalle.Id, CodigoQr = qr, Estado = "VALIDO", FechaEmision = DateTime.UtcNow }); await db.SaveChangesAsync(Ct); }
        return (compra.PublicId, await db.Clientes.Where(c => c.Id == datos.ClienteId).Select(c => c.UsuarioId).SingleAsync(Ct), qr);
    }

    private static async Task<long> OutboxAsync(PromotoraDbContext db, Guid publicId)
    {
        var row = new OutboxMensajeRow
        {
            Agregado = "Compra",
            AgregadoId = publicId.ToString(),
            Tipo = TiposMensaje.CompraCompletada,
            Payload = JsonSerializer.SerializeToDocument(new { publicId }),
            Fecha = DateTime.UtcNow
        };
        db.OutboxMensajes.Add(row); await db.SaveChangesAsync(Ct); return row.Id;
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicion)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await condicion()) await Task.Delay(100, timeout.Token);
    }

    private sealed class CorreoPrueba : IEmailSender
    {
        public int Intentos; public int Fallos; public CancellationTokenSource? Cancelar;
        public Task EnviarAsync(CorreoDto correo, CancellationToken ct)
        {
            Intentos++; Cancelar?.Cancel(); ct.ThrowIfCancellationRequested();
            if (Intentos <= Fallos) throw new CorreoEntregaException(); return Task.CompletedTask;
        }
    }
}
