namespace Promotora.Infrastructure.AI;

public sealed record OllamaOptions
{
    public string ApiKey { private get; init; } = "";
    public string ChatModel { get; init; } = "gpt-oss:120b";
    public string EmbeddingModel { get; init; } = "qwen3-embedding:4b";
    public Uri EmbeddingUrl { get; init; } = new("http://localhost:11434/");
    public int MaxTokensSalida { get; init; } = 512;
    public int TimeoutSegundos { get; init; } = 60;

    internal string Clave => ApiKey;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || ApiKey.Any(char.IsWhiteSpace)
            || string.IsNullOrWhiteSpace(ChatModel) || ChatModel.Length > 100
            || string.IsNullOrWhiteSpace(EmbeddingModel) || EmbeddingModel.Length > 100
            || MaxTokensSalida is < 64 or > 1024 || TimeoutSegundos is < 5 or > 120
            || !EmbeddingUrl.IsAbsoluteUri || !EmbeddingUrl.IsLoopback
            || EmbeddingUrl.Scheme is not ("http" or "https") || EmbeddingUrl.AbsolutePath != "/"
            || EmbeddingUrl.UserInfo.Length != 0 || EmbeddingUrl.Query.Length != 0 || EmbeddingUrl.Fragment.Length != 0)
            throw new InvalidOperationException("Configuración Ollama inválida: clave externa, modelos, límites y endpoint local requeridos.");
    }

    // La representación del objeto nunca incluye credenciales.
    public override string ToString() => $"OllamaOptions {{ ChatModel = {ChatModel}, EmbeddingModel = {EmbeddingModel} }}";
}
