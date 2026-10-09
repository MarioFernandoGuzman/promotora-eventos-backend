using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Sessions;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Identity;
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
var issuer = builder.Configuration["Jwt:Issuer"] ?? "promotora";
var audience = builder.Configuration["Jwt:Audience"] ?? "promotora-api";
byte[]? jwtKey = null;
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora")))
{
    try
    {
        jwtKey = Convert.FromBase64String(builder.Configuration["Jwt:SigningKey"] ?? "");
    }
    catch (FormatException)
    {
        throw new InvalidOperationException("Jwt:SigningKey debe ser una clave externa Base64 de al menos 32 bytes.");
    }

    builder.Services.AddIdentitySessions(jwtKey, issuer, audience, new SesionOptions(
        builder.Configuration.GetValue("Jwt:AccessMinutes", 15), builder.Configuration.GetValue("Jwt:RefreshDays", 7)));
    builder.Services.AddIdentityManagement();
}

builder.Services.AddPromotoraAuthentication(jwtKey, issuer, audience);
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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Promotora de Eventos API",
        Version = "v1",
        Description = "Identidad: registro, altas, login, refresh con rotación y logout. Consulta docs/IDENTIDAD_SEGURIDAD.md para ejemplos y permisos."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    options.OperationFilter<BearerOperationFilter>();
});
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
app.UseAuthentication();
app.UseAuthorization();
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
