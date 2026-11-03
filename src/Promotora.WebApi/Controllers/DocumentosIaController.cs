using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.AI.Documentos;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[Authorize(Policy = IdentityPolicies.Administrador)]
[Authorize(Policy = IdentityPolicies.SesionCompleta)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ValidationProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class DocumentosIaController(ICommandHandler<SubirDocumentoCommand, DocumentoDto> subir,
    IQueryHandler<DocumentosQuery, PaginaDto<DocumentoDto>> listar,
    IQueryHandler<DocumentoQuery, DocumentoDetalleDto> obtener,
    ICommandHandler<DesactivarDocumentoCommand, DocumentoDto> desactivar) : ApiControllerBase
{
    [HttpPost("~/api/v1/ia/documentos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [EndpointSummary("Cargar documento público del negocio para RAG; solo administrador")]
    [EndpointDescription("Formulario titulo y archivo. PDF con texto (sin OCR, hasta 200 páginas), DOCX sin macros, TXT/MD UTF-8. Máximo 5 MiB/archivo y 1 millón de caracteres extraídos. Guarda versión 1 PENDIENTE y audita INGEST; no llama proveedores ni indexa todavía (F9-04). No subir datos personales o privados. Retorna 201 con Location al detalle administrativo.")]
    [ProducesResponseType<DocumentoDto>(201)]
    [ProducesResponseType<ProblemDetails>(413)]
    public Task<ActionResult<DocumentoDto>> Crear([FromForm] DocumentoUploadRequest request, CancellationToken ct) => SubirAsync(request, null, ct);

    [HttpPost("~/api/v1/ia/documentos/{id:int}/versiones")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [EndpointSummary("Crear una nueva versión sin sobrescribir el contenido anterior")]
    [EndpointDescription("Mismo formulario y límites de carga. id debe identificar la última versión de la serie gestionada por esta API; versión anterior/serie histórica sin referencia gestionada: 409. Bloqueo por serie evita duplicar números bajo concurrencia. Retorna nueva fila PENDIENTE (201); las anteriores conservan su estado hasta gestión/indexación posterior.")]
    [ProducesResponseType<DocumentoDto>(201)]
    [ProducesResponseType<ProblemDetails>(413)]
    public Task<ActionResult<DocumentoDto>> NuevaVersion(int id, [FromForm] DocumentoUploadRequest request, CancellationToken ct) => SubirAsync(request, id, ct);

    [HttpGet("~/api/v1/ia/documentos")]
    [EndpointSummary("Listar versiones de documentos; solo administrador")]
    [EndpointDescription("Filtros estado opcional: PENDIENTE/INDEXADO/ERROR/INACTIVO; page 1–100000 y pageSize 1–100 (default 20). Orden fechaCarga/ID descendente. Sin contenido ni rutas internas en el listado.")]
    [ProducesResponseType<PaginaDto<DocumentoDto>>(200)]
    public async Task<ActionResult<PaginaDto<DocumentoDto>>> Listar([FromQuery] DocumentosQuery query, CancellationToken ct) => FromResult(await listar.HandleAsync(query, ct));

    [HttpGet("~/api/v1/ia/documentos/{id:int}")]
    [EndpointSummary("Consultar texto extraído y versiones; solo administrador")]
    [EndpointDescription("Incluye metadatos, contenido extraído y versiones de la misma serie. PDF conserva páginas separadas por \\f; DOCX/TXT/MD no fijan página física. No devuelve archivo original ni ruta de almacenamiento. No disponible públicamente.")]
    [ProducesResponseType<DocumentoDetalleDto>(200)]
    public async Task<ActionResult<DocumentoDetalleDto>> Obtener(int id, CancellationToken ct) => FromResult(await obtener.HandleAsync(new(id), ct));

    [HttpPost("~/api/v1/ia/documentos/{id:int}/desactivar")]
    [EndpointSummary("Desactivar una versión sin borrar documento, texto o chunks")]
    [EndpointDescription("Body: {\"estadoEsperado\":\"INDEXADO\"}. Acepta PENDIENTE/INDEXADO/ERROR; transición condicional a INACTIVO, audita UPDATE. Estado cambió: 409. Ya INACTIVO: 200 sin nueva auditoría. La búsqueda vectorial excluye inmediatamente INACTIVO.")]
    [ProducesResponseType<DocumentoDto>(200)]
    public async Task<ActionResult<DocumentoDto>> Desactivar(int id, DesactivarDocumentoRequest request, CancellationToken ct) => FromResult(await desactivar.HandleAsync(new(id, request.EstadoEsperado), ct));

    [HttpPost("~/api/v1/ia/documentos/{id:int}/reindexar")]
    [EndpointSummary("Indexar o reindexar documento público; administrador habilitado")]
    [EndpointDescription("Sin body. Consume embeddings cloud, máximo 32 fragmentos por documento, lotes de hasta 16. Publicación atómica; al publicar la última versión se retiran las anteriores INDEXADO. Fallar una reindexación conserva el índice publicado. INACTIVO, versión obsoleta o indexación concurrente: 409. IA deshabilitada: 422. Error/cuota del proveedor: 422/429; PENDIENTE pasa a ERROR. Cargar y después llamar esta operación explícitamente.")]
    [ProducesResponseType<DocumentoDto>(200)]
    [ProducesResponseType<ProblemDetails>(422)]
    [ProducesResponseType<ProblemDetails>(429)]
    public async Task<ActionResult<DocumentoDto>> Reindexar(int id, CancellationToken ct)
    {
        var handler = HttpContext.RequestServices.GetService<ICommandHandler<IndexarDocumentoCommand, DocumentoDto>>();
        return handler is null ? FromResult(Result<DocumentoDto>.Failure(new Error("ia.deshabilitada", "IA no está habilitada.", ErrorType.BusinessRule)))
            : FromResult(await handler.HandleAsync(new(id), ct));
    }

    private async Task<ActionResult<DocumentoDto>> SubirAsync(DocumentoUploadRequest request, int? anterior, CancellationToken ct)
    {
        await using var stream = request.Archivo.OpenReadStream();
        var result = await subir.HandleAsync(new(request.Titulo, request.Archivo.FileName, request.Archivo.Length, stream, anterior), ct);
        if (result.IsFailure) return FromResult(result);
        return CreatedAtAction(nameof(Obtener), new { id = result.Value.Id }, result.Value);
    }
}
public sealed class DocumentoUploadRequest
{
    [Required, StringLength(200)] public required string Titulo { get; init; }
    [Required] public required IFormFile Archivo { get; init; }
}
public sealed record DesactivarDocumentoRequest(string EstadoEsperado);
