using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.AI.Ports;
using Promotora.Infrastructure.AI.Persistence;

namespace Promotora.Infrastructure.AI;

public static class AIRegistration
{
    public static IServiceCollection AddOllama(this IServiceCollection services, OllamaOptions options)
    {
        options.Validar();
        services.AddSingleton(options);
        services.AddHttpClient<ILlmPort, OllamaLlmAdapter>(http => http.Timeout = TimeSpan.FromSeconds(options.TimeoutSegundos))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddHttpClient<IEmbeddingPort, OllamaEmbeddingAdapter>(http => http.Timeout = TimeSpan.FromSeconds(options.TimeoutSegundos))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IVectorSearchPort, EfVectorSearchAdapter>();
        return services;
    }
}
