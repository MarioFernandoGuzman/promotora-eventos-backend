using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Documentos;

public sealed class SubirDocumentoHandler(IDocumentoRepository documentos, ITextoDocumentoExtractor extractor,
    DocumentoAccess acceso, IUnitOfWork trabajo, IUsuarioActual actual, IClock clock, IAuditLogger audit)
    : ICommandHandler<SubirDocumentoCommand, DocumentoDto>
{
    public Task<Result<DocumentoDto>> HandleAsync(SubirDocumentoCommand request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (!await acceso.AdministradorAsync(token)) return Result<DocumentoDto>.Failure(DocumentoLimites.Prohibido);
        string referencia = $"db:documentos-rag/{Guid.NewGuid():N}";
        int version = 1;
        if (request.DocumentoAnteriorId is int anteriorId)
        {
            var anterior = await documentos.BloquearSerieAsync(anteriorId, token);
            if (anterior is null) return Result<DocumentoDto>.Failure(DocumentoLimites.NoEncontrado);
            // Las referencias históricas sin una serie gestionada no se reinterpretan como rutas lógicas.
            if (anterior.Referencia is null || !anterior.Referencia.StartsWith("db:documentos-rag/", StringComparison.Ordinal)
                || anterior.Documento.Version != await documentos.UltimaVersionAsync(anterior.Referencia, token)
                || anterior.Documento.Version == int.MaxValue)
                return Result<DocumentoDto>.Failure(DocumentoLimites.Conflicto);
            referencia = anterior.Referencia;
            version = anterior.Documento.Version + 1;
        }
        var texto = await extractor.ExtraerAsync(request.Archivo, request.NombreArchivo, request.Longitud, token);
        if (texto.IsFailure) return Result<DocumentoDto>.Failure(texto.Errors);
        var creado = await documentos.CrearAsync(request.Titulo.Trim(), texto.Value, referencia, version, actual.UsuarioId!.Value, clock.UtcNow, token);
        await audit.WriteAsync(actual.UsuarioId, "INGEST", "documentos_rag", creado.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<DocumentoDto>.Success(creado);
    }, ct);
}
public sealed class DocumentosHandler(IDocumentoRepository documentos, DocumentoAccess acceso, IUnitOfWork trabajo)
    : IQueryHandler<DocumentosQuery, PaginaDto<DocumentoDto>>
{
    public Task<Result<PaginaDto<DocumentoDto>>> HandleAsync(DocumentosQuery request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
        !await acceso.AdministradorAsync(token) ? Result<PaginaDto<DocumentoDto>>.Failure(DocumentoLimites.Prohibido)
        : Result<PaginaDto<DocumentoDto>>.Success(await documentos.ListarAsync(request, token)), ct);
}
public sealed class DocumentoHandler(IDocumentoRepository documentos, DocumentoAccess acceso, IUnitOfWork trabajo)
    : IQueryHandler<DocumentoQuery, DocumentoDetalleDto>
{
    public Task<Result<DocumentoDetalleDto>> HandleAsync(DocumentoQuery request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (!await acceso.AdministradorAsync(token)) return Result<DocumentoDetalleDto>.Failure(DocumentoLimites.Prohibido);
        var documento = await documentos.DetalleAsync(request.Id, token);
        return documento is null ? Result<DocumentoDetalleDto>.Failure(DocumentoLimites.NoEncontrado) : Result<DocumentoDetalleDto>.Success(documento);
    }, ct);
}
public sealed class DesactivarDocumentoHandler(IDocumentoRepository documentos, DocumentoAccess acceso, IUnitOfWork trabajo,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<DesactivarDocumentoCommand, DocumentoDto>
{
    public Task<Result<DocumentoDto>> HandleAsync(DesactivarDocumentoCommand request, CancellationToken ct) => trabajo.ExecuteInTransactionAsync(async token =>
    {
        if (!await acceso.AdministradorAsync(token)) return Result<DocumentoDto>.Failure(DocumentoLimites.Prohibido);
        var guardado = await documentos.BloquearSerieAsync(request.Id, token);
        if (guardado is null) return Result<DocumentoDto>.Failure(DocumentoLimites.NoEncontrado);
        if (guardado.Documento.Estado == "INACTIVO") return Result<DocumentoDto>.Success(guardado.Documento);
        if (!await documentos.DesactivarAsync(request.Id, request.EstadoEsperado, token)) return Result<DocumentoDto>.Failure(DocumentoLimites.Conflicto);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "documentos_rag", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<DocumentoDto>.Success(guardado.Documento with { Estado = "INACTIVO" });
    }, ct);
}
