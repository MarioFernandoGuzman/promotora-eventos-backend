namespace Promotora.Infrastructure.AI;

public sealed record OllamaOptions
{
    public string ApiKey { private get; init; } = "";
    public string ChatModel { get; init; } = "gpt-oss:120b";
    public int MaxTokensSalida { get; init; } = 512;
    public int TimeoutSegundos { get; init; } = 60;

    internal string Clave => ApiKey;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || ApiKey.Any(char.IsWhiteSpace)
            || string.IsNullOrWhiteSpace(ChatModel) || ChatModel.Length > 100
            || MaxTokensSalida is < 64 or > 1024 || TimeoutSegundos is < 5 or > 120)
            throw new InvalidOperationException("Configuración Ollama inválida: clave externa, modelo y límites requeridos.");
    }

    // La representación del objeto nunca incluye credenciales.
    public override string ToString() => $"OllamaOptions {{ ChatModel = {ChatModel} }}";
}
