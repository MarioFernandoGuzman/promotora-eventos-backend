namespace Promotora.Infrastructure.AI;

public sealed record GeminiEmbeddingOptions
{
    public const string Modelo = "gemini-embedding-2";
    public string ApiKey { private get; init; } = "";
    public int TimeoutSegundos { get; init; } = 60;
    internal string Clave => ApiKey;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || ApiKey.Any(char.IsWhiteSpace) || TimeoutSegundos is < 5 or > 120)
            throw new InvalidOperationException("Gemini requiere clave externa y timeout de 5 a 120 segundos.");
    }

    public override string ToString() => $"GeminiEmbeddingOptions {{ Modelo = {Modelo} }}";
}
