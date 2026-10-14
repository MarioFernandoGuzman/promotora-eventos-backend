using System.Globalization;
using System.Text;

namespace Promotora.Application.AI.Documentos;

public sealed record FragmentoDocumento(int Orden, int? Pagina, string Contenido);

public sealed class DocumentoChunker
{
    // Aproximación conservadora: hasta 3200 unidades Unicode (~800 tokens) y 5500 bytes UTF-8;
    // no se afirma conocer el tokenizer del proveedor. Solapamiento de un octavo (~100 tokens); no hay conteo exacto.
    public IReadOnlyList<FragmentoDocumento> Dividir(string contenido, string? tipoDocumento, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contenido) || contenido.Length > DocumentoLimites.MaxCaracteres)
            throw new ArgumentException("Contenido fuera de límites.", nameof(contenido));
        var resultado = new List<FragmentoDocumento>();
        string[] paginas = tipoDocumento == "PDF" ? contenido.Split('\f') : [contenido];
        for (int pagina = 0; pagina < paginas.Length; pagina++)
        {
            var indices = StringInfo.ParseCombiningCharacters(paginas[pagina]);
            for (int inicio = 0; inicio < indices.Length;)
            {
                ct.ThrowIfCancellationRequested();
                int fin = Math.Min(inicio + 3200, indices.Length);
                int Desde(int i) => i == indices.Length ? paginas[pagina].Length : indices[i];
                while (fin > inicio && Encoding.UTF8.GetByteCount(paginas[pagina].AsSpan(Desde(inicio), Desde(fin) - Desde(inicio))) > 5500) fin--;
                if (fin == inicio) throw new ArgumentException("Unidad de texto demasiado grande.", nameof(contenido));
                string texto = paginas[pagina][Desde(inicio)..Desde(fin)].Trim();
                if (texto.Length != 0) resultado.Add(new(resultado.Count + 1, tipoDocumento == "PDF" ? pagina + 1 : null, texto));
                if (resultado.Count > 2000) throw new ArgumentException("Demasiados fragmentos.", nameof(contenido));
                if (fin == indices.Length) break;
                inicio = Math.Max(inicio + 1, fin - Math.Min(400, (fin - inicio) / 8));
            }
        }
        return resultado;
    }
}
