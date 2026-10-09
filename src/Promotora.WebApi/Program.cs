using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Promotora.Application.Identity.Ports;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Shared.Health;
using Promotora.WebApi.Errors;
using Promotora.WebApi.Identity;
using Promotora.WebApi.Middleware;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics", LogEventLevel.Fatal)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"));
builder.Services.AddPromotoraInfrastructure(builder.Configuration.GetConnectionString("Promotora"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, HttpUsuarioActual>();
builder.Services.AddHostedService<BootstrapAdminService>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState)
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Solicitud inválida",
        Type = "about:blank"
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    var response = new BadRequestObjectResult(problem);
    response.ContentTypes.Add("application/problem+json");
    return response;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Promotora de Eventos API",
    Version = "v1",
    Description = "API versionada. Los endpoints de negocio se incorporan desde la Fase 3."
}));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var problem = new ProblemDetails
    {
        Status = response.StatusCode,
        Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(response.StatusCode),
        Type = "about:blank"
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Promotora API v1"));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();
await app.RunAsync();

public partial class Program;
