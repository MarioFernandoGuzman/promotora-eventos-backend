using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotora.Application.Shared.Notifications;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Shared.Notifications;
using Promotora.Infrastructure.Ticketing;
using Promotora.Worker.Messaging;
using Promotora.Worker.Ticketing;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"));
builder.Services.AddPromotoraInfrastructure(builder.Configuration.GetConnectionString("Promotora"));
builder.Services.AddPagoSimulado(new FakePaymentOptions(builder.Configuration.GetValue("PagosSimulados:TasaFallo", 0m)));
if (!string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]))
{
    builder.Services.AddRabbitMqMessaging(new RabbitMqOptions
    {
        Host = builder.Configuration["RabbitMQ:Host"]!,
        Port = builder.Configuration.GetValue("RabbitMQ:Port", 5672),
        VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
        Username = builder.Configuration["RabbitMQ:Username"] ?? "",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "",
        PublishTimeoutSeconds = builder.Configuration.GetValue("RabbitMQ:PublishTimeoutSeconds", 15)
    });
}
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora")))
{
    builder.Services.AddEmisionBoletosBase64(builder.Configuration["Boletos:SigningKey"], builder.Configuration.GetValue<int?>("Boletos:CategoriaCarreraId"));
    builder.Services.AddFinalizacionCompras();
    builder.Services.AddExpiracionCompras();
    builder.Services.AddProcesamientoCompras();
}
if (builder.Configuration.GetValue("Outbox:Enabled", false))
{
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora"))
        || string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]))
        throw new InvalidOperationException("Outbox habilitado requiere conexión PostgreSQL y RabbitMQ externos.");
    var relayOptions = new OutboxRelayOptions(builder.Configuration.GetValue("Outbox:BatchSize", 50),
        builder.Configuration.GetValue("Outbox:PollIntervalSeconds", 2));
    relayOptions.Validar();
    builder.Services.AddSingleton(relayOptions);
    builder.Services.AddOutboxRelay();
    builder.Services.AddHostedService<OutboxRelayService>();
}
if (builder.Configuration.GetValue("Compras:ConsumerEnabled", false))
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"])
        || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora"))
        || string.IsNullOrWhiteSpace(builder.Configuration["Boletos:SigningKey"])
        || builder.Configuration.GetValue<int?>("Boletos:CategoriaCarreraId") is null)
        throw new InvalidOperationException("Consumidor requiere PostgreSQL, RabbitMQ, firma QR y categoría de carreras externos.");
    builder.Services.AddCompraConsumer(new CompraConsumerOptions(
        builder.Configuration.GetValue<ushort>("Compras:Prefetch", 16), builder.Configuration.GetValue("Compras:MaxIntentos", 3),
        builder.Configuration.GetValue("Compras:RetryBaseDelayMilliseconds", 5000)));
    builder.Services.AddHostedService<ProcesarCompraConsumerService>();
}
if (builder.Configuration.GetValue("Notificaciones:Enabled", false))
{
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora"))
        || string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]))
        throw new InvalidOperationException("Notificaciones requieren PostgreSQL y RabbitMQ externos.");
    builder.Services.AddConfirmacionNotificaciones(new SmtpOptions(builder.Configuration["Smtp:Host"] ?? "",
        builder.Configuration.GetValue("Smtp:Port", 1025), builder.Configuration["Smtp:From"] ?? "",
        builder.Configuration.GetValue("Smtp:EnableSsl", false), builder.Configuration["Smtp:Username"], builder.Configuration["Smtp:Password"],
        builder.Configuration.GetValue("Smtp:TimeoutSeconds", 15)), new NotificacionOptions(
        builder.Configuration.GetValue("Notificaciones:MaxIntentos", 3), builder.Configuration.GetValue("Notificaciones:RetryDelayMilliseconds", 2000)));
    builder.Services.AddHostedService<NotificacionConsumerService>();
    builder.Services.AddBienvenidaNotificaciones();
}
if (builder.Configuration.GetValue("Expiracion:Enabled", false))
{
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora")))
        throw new InvalidOperationException("Expiración habilitada requiere conexión PostgreSQL externa.");
    builder.Services.AddExpiracionWorker(new(builder.Configuration.GetValue("Expiracion:BatchSize", 100),
        builder.Configuration.GetValue("Expiracion:PollIntervalSeconds", 30)));
}
var host = builder.Build();
await using var hostLifetime = (IAsyncDisposable)host;
await host.RunAsync();
