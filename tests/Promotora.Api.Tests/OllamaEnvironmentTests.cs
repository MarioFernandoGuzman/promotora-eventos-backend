using Promotora.WebApi.AI;

namespace Promotora.Api.Tests;

public sealed class OllamaEnvironmentTests
{
    [Theory]
    [InlineData("OLLAMA_API_KEY=valor-simulado", "valor-simulado")]
    [InlineData("# comentario\nOLLAMA_API_KEY='valor-simulado'\nOTRA_VARIABLE=ignorar", "valor-simulado")]
    [InlineData("OLLAMA_API_KEY=\"valor-simulado\"", "valor-simulado")]
    [InlineData("OTRA_VARIABLE=ignorar", null)]
    public async Task LeeSoloClaveIndicadaSinExpandirOtrasVariables(string contenido, string? esperado)
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, contenido, TestContext.Current.CancellationToken);
            Assert.Equal(esperado, OllamaEnvironmentFile.LeerClave(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("OLLAMA_API_KEY=")]
    [InlineData("OLLAMA_API_KEY=valor con espacios")]
    [InlineData("OLLAMA_API_KEY=uno\nOLLAMA_API_KEY=dos")]
    public async Task ConfiguracionAmbiguaFallaSinExponerValores(string contenido)
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, contenido, TestContext.Current.CancellationToken);
            var error = Assert.Throws<InvalidOperationException>(() => OllamaEnvironmentFile.LeerClave(path));
            Assert.DoesNotContain("uno", error.Message);
            Assert.DoesNotContain("dos", error.Message);
        }
        finally { File.Delete(path); }
    }
}
