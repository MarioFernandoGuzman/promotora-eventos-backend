using System.Globalization;
using FluentValidation;
using Promotora.Application.AI.Ports;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Documentos;

public sealed record IndexarDocumentoCommand(int Id) : ICommand<DocumentoDto>;
public sealed record DocumentoParaIndexar(DocumentoDto Documento, string Contenido, string? Referencia);
public sealed record FragmentoVectorizado(FragmentoDocumento Fragmento, float[] Vector, string Modelo);
public interface IIndexacionRepository
{
    Task<DocumentoParaIndexar?> ObtenerAsync(int id, CancellationToken ct);
    Task<IAsyncDisposable?> IntentarBloquearAsync(int id, CancellationToken ct);
    Task<bool> PublicarAsync(DocumentoParaIndexar documento, IReadOnlyList<FragmentoVectorizado> fragmentos, CancellationToken ct);
    Task MarcarErrorAsync(int id, CancellationToken ct);
}
public sealed class IndexarDocumentoValidator : AbstractValidator<IndexarDocumentoCommand>
{
    public IndexarDocumentoValidator() => RuleFor(x => x.Id).GreaterThan(0);
}
public sealed class IndexarDocumentoHandler(IIndexacionRepository repositorio, IDocumentoRepository documentos, IEmbeddingPort embeddings, DocumentoChunker chunker,
    DocumentoAccess acceso, IUnitOfWork trabajo, IUsuarioActual actual, IAuditLogger auditoria) : ICommandHandler<IndexarDocumentoCommand, DocumentoDto>
{
    public async Task<Result<DocumentoDto>> HandleAsync(IndexarDocumentoCommand request, CancellationToken ct)
    {
        var preparado = await trabajo.ExecuteInTransactionAsync(async token =>
        {
            if (!await acceso.AdministradorAsync(token)) return Result<DocumentoParaIndexar>.Failure(DocumentoLimites.Prohibido);
            var documento = await repositorio.ObtenerAsync(request.Id, token);
            if (documento is null) return Result<DocumentoParaIndexar>.Failure(DocumentoLimites.NoEncontrado);
            if (documento.Referencia is not null && documento.Documento.Version != await documentos.UltimaVersionAsync(documento.Referencia, token))
                return Result<DocumentoParaIndexar>.Failure(DocumentoLimites.Conflicto);
            return documento.Documento.Estado == "INACTIVO" ? Result<DocumentoParaIndexar>.Failure(DocumentoLimites.Conflicto) : Result<DocumentoParaIndexar>.Success(documento);
        }, ct);
        if (preparado.IsFailure) return Result<DocumentoDto>.Failure(preparado.Errors);
        await using var bloqueo = await repositorio.IntentarBloquearAsync(request.Id, ct);
        if (bloqueo is null) return Result<DocumentoDto>.Failure(DocumentoLimites.Conflicto);
        IReadOnlyList<FragmentoDocumento> fragmentos;
        try { fragmentos = chunker.Dividir(preparado.Value.Contenido, preparado.Value.Documento.TipoDocumento, ct); }
        catch (ArgumentException) { return await ErrorAsync(DocumentoLimites.Invalido, ct); }
        // Tope deliberado de consumo: no convertir un archivo enorme en cientos de llamadas gratuitas.
        if (fragmentos.Count is < 1 or > 32) return await ErrorAsync(new Error("ia.fragmentos", "El documento requiere de 1 a 32 fragmentos; divide los documentos más grandes.", ErrorType.Validation), ct);
        var vectorizados = new List<FragmentoVectorizado>();
        foreach (var lote in fragmentos.Chunk(16))
        {
            var resultado = await embeddings.GenerarAsync(lote.Select(f => f.Contenido).ToArray(), FinalidadEmbedding.Documento, ct);
            if (resultado.IsFailure) return await ErrorAsync(resultado.Errors[0], ct);
            if (resultado.Value.Vectores.Count != lote.Length || string.IsNullOrWhiteSpace(resultado.Value.Modelo))
                return await ErrorAsync(DocumentoLimites.Invalido, ct);
            for (int i = 0; i < lote.Length; i++)
            {
                float[] vector = resultado.Value.Vectores[i];
                if (vector.Length != PoliticaAsistente.Dimensiones || vector.Any(v => !float.IsFinite(v)) || vector.All(v => v == 0))
                    return await ErrorAsync(DocumentoLimites.Invalido, ct);
                vectorizados.Add(new(lote[i], vector, resultado.Value.Modelo));
            }
        }
        if (vectorizados.Select(f => f.Modelo).Distinct().Count() != 1) return await ErrorAsync(DocumentoLimites.Invalido, ct);
        return await trabajo.ExecuteInTransactionAsync(async token =>
        {
            if (!await acceso.AdministradorAsync(token)) return Result<DocumentoDto>.Failure(DocumentoLimites.Prohibido);
            if (!await repositorio.PublicarAsync(preparado.Value, vectorizados, token)) return Result<DocumentoDto>.Failure(DocumentoLimites.Conflicto);
            await auditoria.WriteAsync(actual.UsuarioId, "INGEST", "documentos_rag", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            return Result<DocumentoDto>.Success(preparado.Value.Documento with { Estado = "INDEXADO" });
        }, ct);

        async Task<Result<DocumentoDto>> ErrorAsync(Error error, CancellationToken token)
        {
            await trabajo.ExecuteInTransactionAsync(async inner =>
        {
            await repositorio.MarcarErrorAsync(request.Id, inner);
            await auditoria.WriteAsync(actual.UsuarioId, "INGEST", "documentos_rag", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, inner);
            // Confirmar la marca ERROR; el fallo HTTP se devuelve fuera de la transacción.
            return Result<DocumentoDto>.Success(preparado.Value.Documento with { Estado = "ERROR" });
        }, token);
            return Result<DocumentoDto>.Failure(error);
        }
    }
}
