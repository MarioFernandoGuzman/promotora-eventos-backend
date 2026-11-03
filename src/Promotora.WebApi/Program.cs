using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Ticketing;
using Promotora.Infrastructure;
using Promotora.Infrastructure.AI;
using Promotora.Infrastructure.Catalog;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Images;
using Promotora.Infrastructure.Reporting;
using Promotora.Infrastructure.Shared.Health;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Venues;
using Promotora.WebApi.Catalog;
using Promotora.WebApi.Errors;
using Promotora.WebApi.Identity;
using Promotora.WebApi.Images;
using Promotora.WebApi.Middleware;
using Promotora.WebApi.Ticketing;
using Promotora.WebApi.Venues;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration.GetValue("IA:Habilitada", false))
{
    string? clave = builder.Configuration["Ollama:ApiKey"] ?? builder.Configuration["OLLAMA_API_KEY"];
    string? claveGemini = builder.Configuration["Gemini:ApiKey"] ?? builder.Configuration["GEMINI_API_KEY"];
    if (builder.Environment.IsDevelopment())
    {
        string envPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env"));
        if (string.IsNullOrWhiteSpace(clave)) clave = Promotora.WebApi.AI.OllamaEnvironmentFile.LeerClave(envPath);
        if (string.IsNullOrWhiteSpace(claveGemini)) claveGemini = Promotora.WebApi.AI.OllamaEnvironmentFile.LeerClave(envPath, "GEMINI_API_KEY");
    }
    builder.Services.AddOllama(new OllamaOptions
    {
        ApiKey = clave ?? "",
        ChatModel = builder.Configuration["Ollama:ChatModel"] ?? "gpt-oss:120b",
        MaxTokensSalida = builder.Configuration.GetValue("Ollama:MaxTokensSalida", 512),
        TimeoutSegundos = builder.Configuration.GetValue("Ollama:TimeoutSegundos", 60)
    }, new GeminiEmbeddingOptions
    {
        ApiKey = claveGemini ?? "",
        TimeoutSegundos = builder.Configuration.GetValue("Gemini:TimeoutSegundos", 60)
    }, new Promotora.Application.AI.Rag.RagOptions(builder.Configuration.GetValue("Rag:TopK", 3), builder.Configuration.GetValue("Rag:SimilitudMinima", 0.65)));
}
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics", LogEventLevel.Fatal)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"));
builder.Services.AddPromotoraInfrastructure(builder.Configuration.GetConnectionString("Promotora"));
builder.Services.AddFinanzas(builder.Configuration.GetValue("Finanzas:TasaIva", 0.12m), builder.Configuration.GetValue("Finanzas:ComisionPorcentaje", 0.10m));
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Promotora")))
    builder.Services.AddPagoSimuladoDurable(new FakePaymentOptions(builder.Configuration.GetValue("PagosSimulados:TasaFallo", 0m)), builder.Configuration.GetConnectionString("Promotora")!);
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
    builder.Services.AddDocumentosRag();
    builder.Services.AddIdentityRegistration();
    builder.Services.AddCuentasFinancieras();
    builder.Services.AddReporting();
    Promotora.Infrastructure.Reviews.ReviewsRegistration.AddReviews(builder.Services);
    builder.Services.AddReembolsos();
    builder.Services.AddVenues();
    builder.Services.AddCatalog();
    builder.Services.AddTicketing(new CompraOptions(builder.Configuration.GetValue("Ticketing:ReservaMinutos", 15)));
    builder.Services.AddNumeracion();
    builder.Services.AddEmisionBoletosBase64(builder.Configuration["Boletos:SigningKey"], builder.Configuration.GetValue<int?>("Boletos:CategoriaCarreraId"));
    builder.Services.AddFinalizacionCompras();
    builder.Services.AddImages(builder.Configuration["Images:RootPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "uploads"));
}

builder.Services.AddPromotoraAuthentication(jwtKey, issuer, audience);
builder.Services.AddIdentityHttpProtection(builder.Configuration);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
builder.Services.AddControllers(options => options.Conventions.Add(new Microsoft.AspNetCore.Mvc.ApplicationModels.RouteTokenTransformerConvention(new LowercaseRouteTransformer())))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
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
    options.OperationFilter<ImagenesOperationFilter>();
    options.SchemaFilter<IdentityExamplesFilter>();
    options.SchemaFilter<VenuesExamplesFilter>();
    options.SchemaFilter<CatalogExamplesFilter>();
    options.SchemaFilter<CompraExamplesFilter>();
    options.SchemaFilter<Promotora.WebApi.Finance.FinanzasExamplesFilter>();
    options.SchemaFilter<Promotora.WebApi.Reviews.ReviewsExamplesFilter>();
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
app.UseRouting();
app.UseCors(IdentityHttpProtection.Cors);
app.UseRateLimiter();
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
