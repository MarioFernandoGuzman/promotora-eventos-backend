using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.AI.Ports;
using FluentValidation;
using Promotora.Application.AI.Documentos;
using Promotora.Infrastructure.Shared;
using Promotora.Application.AI.Rag;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Application.AI.Consultas;
using Promotora.Infrastructure.AI.Persistence;

namespace Promotora.Infrastructure.AI;

public static class AIRegistration
{
    public static IServiceCollection AddOllama(this IServiceCollection services, OllamaOptions options, GeminiEmbeddingOptions embeddings)
    {
        options.Validar();
        embeddings.Validar();
        services.AddSingleton(options);
        services.AddSingleton(embeddings);
        services.AddHttpClient<ILlmPort, OllamaLlmAdapter>(http => http.Timeout = TimeSpan.FromSeconds(options.TimeoutSegundos))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddHttpClient<IEmbeddingPort, GeminiEmbeddingAdapter>(http => http.Timeout = TimeSpan.FromSeconds(embeddings.TimeoutSegundos))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IVectorSearchPort>(provider => new EfVectorSearchAdapter(provider.GetRequiredService<PromotoraDbContext>(), GeminiEmbeddingOptions.Modelo));
        services.AddSingleton(new RagOptions());
        services.AddSingleton<RespuestaFundamentada>();
        services.AddScoped<RecuperarContexto>();
        services.AddScoped<IValidator<ConsultarAgenteCommand>, ConsultarAgenteValidator>();
        services.AddCommandHandler<ConsultarAgenteCommand, ConsultaIaDto, ConsultarAgenteHandler>();
        services.AddSingleton<DocumentoChunker>();
        services.AddScoped<IIndexacionRepository, EfIndexacionRepository>();
        services.AddScoped<IValidator<IndexarDocumentoCommand>, IndexarDocumentoValidator>();
        services.AddCommandHandler<IndexarDocumentoCommand, DocumentoDto, IndexarDocumentoHandler>();
        return services;
    }
}
