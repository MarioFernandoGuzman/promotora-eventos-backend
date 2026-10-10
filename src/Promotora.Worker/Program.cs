using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Ticketing;
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
using var host = builder.Build();
await host.RunAsync();
