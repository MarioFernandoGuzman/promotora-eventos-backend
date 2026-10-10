using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Notifications;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Notifications;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing;

namespace Promotora.Infrastructure.Tests;

public sealed class BienvenidaNotificacionTests(PostgresFixture postgres, RabbitMqFixture rabbit, MailHogFixture mail)
    : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>, IClassFixture<MailHogFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RegistroRealOutboxRelayRabbitMqYMailHogUsanCorreoActualDeBd()
    {
        var usuario = await UsuarioAsync();
        await using var db = postgres.CreateContext();
        var evento = await db.OutboxMensajes.SingleAsync(m => m.Tipo == TiposMensaje.UsuarioRegistrado && m.AgregadoId == usuario.Id.ToString(), Ct);
        string nuevoEmail = $"{Guid.NewGuid():N}@example.invalid";
        await db.Usuarios.Where(u => u.Id == usuario.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, nuevoEmail), Ct);
        var services = new ServiceCollection(); services.AddPromotoraInfrastructure(postgres.ConnectionString);
        services.AddSingleton(rabbit.Options); services.AddEmisionBoletos(null);
        services.AddConfirmacionNotificaciones(mail.Options, new(3, 1)); services.AddBienvenidaNotificaciones();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var tasks = provider.GetServices<INotificacionConsumer>().Select(c => c.EjecutarAsync(stop.Token)).ToArray();
        try
        {
            await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
            var relay = new EfOutboxRelay(db, publisher, new SystemClock(TimeProvider.System));
            await relay.PublicarLoteAsync(50, Ct);
            await publisher.PublicarAsync(new(evento.Id, TiposMensaje.UsuarioRegistrado,
                System.Text.Encoding.UTF8.GetBytes(evento.Payload.RootElement.GetRawText())), Ct);
            await EsperarAsync(async () => await db.MensajesProcesados.AnyAsync(m => m.MensajeId == evento.Id && m.Consumidor == "EnviarBienvenidaConsumer", Ct)
                && await rabbit.EstadoColaAsync("notificaciones.registro", Ct) == (0, 0));
            var notificacion = await db.Notificaciones.SingleAsync(n => n.UsuarioId == usuario.Id, Ct);
            Assert.Equal("ENVIADO", notificacion.Estado); Assert.NotNull(notificacion.FechaEnvio);
            using var correos = await mail.MensajesAsync(Ct);
            var item = Assert.Single(correos.RootElement.GetProperty("items").EnumerateArray(), i => i.GetProperty("Content").GetProperty("Headers")
                .GetProperty("Message-ID")[0].GetString() == $"<usuario.registrado-{evento.Id}@promotora.invalid>");
            Assert.Contains(nuevoEmail, item.GetProperty("Content").GetProperty("Headers").GetProperty("To")[0].GetString());
            Assert.DoesNotContain(usuario.Email, item.GetProperty("Content").GetProperty("Headers").GetProperty("To")[0].GetString());
        }
        finally
        {
            stop.Cancel();
            foreach (var task in tasks) { try { await task.WaitAsync(TimeSpan.FromSeconds(15), Ct); } catch (OperationCanceledException) { } }
        }
    }

    [Fact]
    public async Task DuplicadosConcurrentesCompartenUnaNotificacionYUnEnvio()
    {
        var usuario = await UsuarioAsync(); await using var db = postgres.CreateContext(); long id = await MensajeIdAsync(db, usuario.Id);
        var sender = new CorreoPrueba();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var contexto = postgres.CreateContext(); await Handler(contexto, sender).ProcesarAsync(new(id, usuario.Id, true), Ct);
        }));
        Assert.Equal(1, sender.Intentos);
        Assert.Equal(1, await db.Notificaciones.CountAsync(n => n.UsuarioId == usuario.Id, Ct));
    }

    [Theory]
    [InlineData(1, "ENVIADO", 2)]
    [InlineData(99, "FALLIDO", 3)]
    public async Task FallosSmtpTienenReintentosAcotados(int fallos, string estado, int intentos)
    {
        var usuario = await UsuarioAsync(); await using var db = postgres.CreateContext(); long id = await MensajeIdAsync(db, usuario.Id);
        var sender = new CorreoPrueba { Fallos = fallos };
        await Handler(db, sender).ProcesarAsync(new(id, usuario.Id, true), Ct);
        await Handler(db, sender).ProcesarAsync(new(id, usuario.Id, true), Ct);
        Assert.Equal(intentos, sender.Intentos);
        Assert.Equal(estado, (await db.Notificaciones.SingleAsync(n => n.UsuarioId == usuario.Id, Ct)).Estado);
    }

    [Fact]
    public async Task UsuarioInactivoNoRecibeCorreo()
    {
        var usuario = await UsuarioAsync(); await using var db = postgres.CreateContext();
        await db.Usuarios.Where(u => u.Id == usuario.Id && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        long id = await MensajeIdAsync(db, usuario.Id); var sender = new CorreoPrueba();
        await Handler(db, sender).ProcesarAsync(new(id, usuario.Id, true), Ct);
        Assert.Equal(0, sender.Intentos);
        Assert.True(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct));
    }

    [Fact]
    public async Task MensajeInvalidoQuedaFallidoSinEnviar()
    {
        await using var db = postgres.CreateContext(); long id = await MensajeIdAsync(db, null); var sender = new CorreoPrueba();
        await Handler(db, sender).ProcesarAsync(new(id, null, false), Ct);
        Assert.Equal(0, sender.Intentos);
        Assert.True(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct));
    }

    [Fact]
    public async Task CancelacionRevierteMensajeProcesadoYPermiteReintentar()
    {
        var usuario = await UsuarioAsync(); await using var db = postgres.CreateContext(); long id = await MensajeIdAsync(db, usuario.Id);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct); var sender = new CorreoPrueba { Cancelar = stop };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler(db, sender).ProcesarAsync(new(id, usuario.Id, true), stop.Token));
        Assert.False(await db.MensajesProcesados.AnyAsync(m => m.MensajeId == id, Ct));
        Assert.False(await db.Notificaciones.AnyAsync(n => n.UsuarioId == usuario.Id, Ct));
        await Handler(db, new CorreoPrueba()).ProcesarAsync(new(id, usuario.Id, true), Ct);
        Assert.True(await db.Notificaciones.AnyAsync(n => n.UsuarioId == usuario.Id && n.Estado == "ENVIADO", Ct));
    }

    private EnviarBienvenidaHandler Handler(PromotoraDbContext db, IEmailSender sender)
    {
        var clock = new SystemClock(TimeProvider.System);
        return new(new EfNotificacionRepository(db, new EfEmisionBoletosRepository(db, new()), clock), new EfMensajeProcesadoRepository(db, clock),
            new EfUnitOfWork(db), sender, new(3, 1));
    }

    private async Task<(int Id, string Email)> UsuarioAsync()
    {
        await using var db = postgres.CreateContext(); var clock = new SystemClock(TimeProvider.System);
        var repo = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, clock));
        var email = $"{Guid.NewGuid():N}@example.invalid";
        var result = await repo.CreateAsync(new(0, email, "!SIN_ACCESO_PRUEBA!", "Cliente", "Prueba", RolUsuario.CLIENTE, "ACTIVO", false, clock.UtcNow),
            new PerfilNuevo(), null, null, false, Ct);
        Assert.True(result.IsSuccess); return (result.Value.Id, email);
    }

    private static async Task<long> MensajeIdAsync(PromotoraDbContext db, int? usuarioId)
    {
        var row = new OutboxMensajeRow
        {
            Agregado = "usuarios",
            AgregadoId = usuarioId?.ToString() ?? "invalido",
            Tipo = TiposMensaje.UsuarioRegistrado,
            Payload = JsonSerializer.SerializeToDocument(new { usuarioId }),
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
