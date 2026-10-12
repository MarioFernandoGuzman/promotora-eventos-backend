namespace Promotora.WebApi.AI;

// Lector acotado: solo OLLAMA_API_KEY; no ejecuta ni expande contenido del archivo.
public static class OllamaEnvironmentFile
{
    public static string? LeerClave(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 256 * 1024) throw new InvalidOperationException("El archivo .env excede el tamaño permitido.");
        string? clave = null;
        foreach (string line in File.ReadLines(path))
        {
            string texto = line.Trim();
            int separador = texto.IndexOf('=');
            if (separador < 0 || texto[..separador].Trim() != "OLLAMA_API_KEY") continue;
            if (clave is not null) throw new InvalidOperationException("OLLAMA_API_KEY está duplicada en .env.");
            string valor = texto[(separador + 1)..].Trim();
            if (valor.Length >= 2 && valor[0] is '\'' or '"' && valor[^1] == valor[0]) valor = valor[1..^1];
            if (string.IsNullOrWhiteSpace(valor) || valor.Any(char.IsWhiteSpace) || valor.Contains('"') || valor.Contains('\''))
                throw new InvalidOperationException("OLLAMA_API_KEY en .env debe contener un valor simple no vacío.");
            clave = valor;
        }
        return clave;
    }
}
