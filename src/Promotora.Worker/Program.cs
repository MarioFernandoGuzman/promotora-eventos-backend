using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Finance;
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
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora")))
    builder.Services.AddEmisionBoletosBase64(builder.Configuration["Boletos:SigningKey"], builder.Configuration.GetValue<int?>("Boletos:CategoriaCarreraId"));
using var host = builder.Build();
await host.RunAsync();
