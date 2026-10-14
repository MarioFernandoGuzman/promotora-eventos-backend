using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Documentos;

public sealed record DocumentoDto(int Id, string Titulo, string? TipoDocumento, int Version, string Estado, DateTime FechaCarga, int SubidoPor);
public sealed record DocumentoDetalleDto(DocumentoDto Documento, string? Contenido, IReadOnlyList<DocumentoDto> Versiones);
public sealed record DocumentoGuardado(DocumentoDto Documento, string? Referencia);
public sealed record TextoExtraido(string Contenido, string TipoDocumento);
public sealed record SubirDocumentoCommand(string Titulo, string NombreArchivo, long Longitud, Stream Archivo, int? DocumentoAnteriorId = null) : ICommand<DocumentoDto>;
public sealed record DocumentosQuery(int Page = 1, int PageSize = 20, string? Estado = null) : IQuery<PaginaDto<DocumentoDto>>;
public sealed record DocumentoQuery(int Id) : IQuery<DocumentoDetalleDto>;
public sealed record DesactivarDocumentoCommand(int Id, string EstadoEsperado) : ICommand<DocumentoDto>;

public interface ITextoDocumentoExtractor
{
    Task<Result<TextoExtraido>> ExtraerAsync(Stream archivo, string nombreArchivo, long longitud, CancellationToken ct);
}
public interface IDocumentoRepository
{
    Task<PaginaDto<DocumentoDto>> ListarAsync(DocumentosQuery query, CancellationToken ct);
    Task<DocumentoDetalleDto?> DetalleAsync(int id, CancellationToken ct);
    Task<DocumentoGuardado?> BloquearSerieAsync(int id, CancellationToken ct);
    Task<int> UltimaVersionAsync(string referencia, CancellationToken ct);
    Task<DocumentoDto> CrearAsync(string titulo, TextoExtraido texto, string referencia, int version, int usuario, DateTime fecha, CancellationToken ct);
    Task<bool> DesactivarAsync(int id, string esperado, CancellationToken ct);
}
public static class DocumentoLimites
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxCaracteres = 1_000_000;
    public static bool EstadoValido(string? estado) => estado is "PENDIENTE" or "INDEXADO" or "ERROR" or "INACTIVO";
    public static readonly Error Invalido = new("documento.invalido", "Archivo inválido, sin texto o fuera de los límites permitidos. Usa PDF con texto, DOCX, TXT o MD UTF-8.", ErrorType.Validation);
    public static readonly Error Prohibido = new("documento.prohibido", "Se requiere un administrador habilitado.", ErrorType.Forbidden);
    public static readonly Error NoEncontrado = new("documento.no_encontrado", "Documento no encontrado.", ErrorType.NotFound);
    public static readonly Error Conflicto = new("documento.conflicto", "La versión o el estado cambió. Recarga el documento.", ErrorType.Conflict);
}
