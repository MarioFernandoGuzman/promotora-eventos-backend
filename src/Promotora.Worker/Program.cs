using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Ticketing;
using Promotora.Worker.Messaging;
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
    builder.Services.AddExpiracionCompras();
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
var host = builder.Build();
await using var hostLifetime = (IAsyncDisposable)host;
await host.RunAsync();
