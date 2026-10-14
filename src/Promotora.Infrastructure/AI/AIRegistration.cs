using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.AI.Consultas;
using Promotora.Application.AI.Documentos;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Infrastructure.AI.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.AI;

public static class AIRegistration
{
    public static IServiceCollection AddOllama(this IServiceCollection services, OllamaOptions options, GeminiEmbeddingOptions embeddings, RagOptions? rag = null)
    {
        options.Validar();
        embeddings.Validar();
        rag ??= new RagOptions();
        rag.Validar();
        services.AddSingleton(options);
        services.AddSingleton(embeddings);
        services.AddHttpClient<ILlmPort, OllamaLlmAdapter>(http => http.Timeout = TimeSpan.FromSeconds(options.TimeoutSegundos))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddHttpClient<IEmbeddingPort, GeminiEmbeddingAdapter>(http => http.Timeout = TimeSpan.FromSeconds(embeddings.TimeoutSegundos))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IVectorSearchPort>(provider => new EfVectorSearchAdapter(provider.GetRequiredService<PromotoraDbContext>(), GeminiEmbeddingOptions.Modelo));
        services.AddSingleton(rag);
        services.AddSingleton<RespuestaFundamentada>();
        services.AddScoped<RecuperarContexto>();
        services.AddScoped<IInformacionPublicaPort, EfInformacionPublicaPort>();
        services.AddScoped<IValidator<ConsultarAgenteCommand>, ConsultarAgenteValidator>();
        services.AddScoped<ConsultarAgenteHandler>();
        services.AddScoped<IConsultaIaLogPort, EfConsultaIaLogPort>();
        services.AddCommandHandler<ConsultarAgenteCommand, ConsultaIaDto, RegistrarConsultaHandler>();
        services.AddSingleton<DocumentoChunker>();
        services.AddScoped<IIndexacionRepository, EfIndexacionRepository>();
        services.AddScoped<IValidator<IndexarDocumentoCommand>, IndexarDocumentoValidator>();
        services.AddCommandHandler<IndexarDocumentoCommand, DocumentoDto, IndexarDocumentoHandler>();
        return services;
    }
}
