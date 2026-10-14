using System.IO.Compression;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Promotora.Application.AI.Documentos;
using Promotora.Domain.Shared;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Promotora.Infrastructure.AI;

public sealed class TextoDocumentoExtractor : ITextoDocumentoExtractor
{
    public async Task<Result<TextoExtraido>> ExtraerAsync(Stream archivo, string nombreArchivo, long longitud, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (longitud is < 1 or > DocumentoLimites.MaxBytes || extension is not (".pdf" or ".docx" or ".txt" or ".md"))
            return Result<TextoExtraido>.Failure(DocumentoLimites.Invalido);
        try
        {
            using var copia = new MemoryStream();
            byte[] buffer = new byte[8192];
            int leidos;
            while ((leidos = await archivo.ReadAsync(buffer, ct)) != 0)
            {
                if (copia.Length + leidos > DocumentoLimites.MaxBytes) return Result<TextoExtraido>.Failure(DocumentoLimites.Invalido);
                await copia.WriteAsync(buffer.AsMemory(0, leidos), ct);
            }
            if (copia.Length != longitud) return Result<TextoExtraido>.Failure(DocumentoLimites.Invalido);
            copia.Position = 0;
            string texto = extension switch
            {
                ".pdf" => Pdf(copia, ct),
                ".docx" => Docx(copia, ct),
                _ => new UTF8Encoding(false, true).GetString(copia.ToArray()).TrimStart('\uFEFF')
            };
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(texto) || texto.Length > DocumentoLimites.MaxCaracteres
                || texto.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t' or '\f')))
                return Result<TextoExtraido>.Failure(DocumentoLimites.Invalido);
            return Result<TextoExtraido>.Success(new(texto, extension[1..].ToUpperInvariant()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // Los parsers de archivos malformados lanzan distintas excepciones. No devolver
            // texto del archivo, rutas ni detalles del parser al cliente o al log.
            return Result<TextoExtraido>.Failure(DocumentoLimites.Invalido);
        }
    }
    private static string Pdf(Stream stream, CancellationToken ct)
    {
        using var documento = PdfDocument.Open(stream);
        if (documento.IsEncrypted || documento.NumberOfPages > 200) throw new InvalidDataException();
        var texto = new StringBuilder();
        foreach (var pagina in documento.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            // Separador de página reservado para que F9-03 conserve la página de la cita.
            if (pagina.Number > 1) texto.Append('\f');
            Agregar(texto, ContentOrderTextExtractor.GetText(pagina).Replace('\f', ' '));
        }
        return texto.ToString();
    }
    private static string Docx(Stream stream, CancellationToken ct)
    {
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (zip.Entries.Count > 500 || zip.Entries.Sum(e => e.Length) > 20 * 1024 * 1024) throw new InvalidDataException();
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
                // Prevalidar TODO XML del paquete antes de OpenXml: DTD y entidades externas prohibidos.
                if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    && !entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                using var contenido = entry.Open();
                using var reader = XmlReader.Create(contenido, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20 * 1024 * 1024 });
                while (reader.Read()) ct.ThrowIfCancellationRequested();
            }
        }
        stream.Position = 0;
        using var documento = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 20 * 1024 * 1024 });
        if (documento.DocumentType != DocumentFormat.OpenXml.WordprocessingDocumentType.Document) throw new InvalidDataException();
        var texto = new StringBuilder();
        foreach (var parrafo in documento.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>() ?? [])
        {
            ct.ThrowIfCancellationRequested();
            Agregar(texto, parrafo.InnerText);
            texto.AppendLine();
        }
        // DOCX no fija paginación física; no inventar números de página desde saltos renderizados.
        return texto.ToString();
    }
    private static void Agregar(StringBuilder destino, string texto)
    {
        if (destino.Length + texto.Length > DocumentoLimites.MaxCaracteres) throw new InvalidDataException();
        destino.Append(texto);
    }
}
