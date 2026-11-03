using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Images;
using Promotora.WebApi.Identity;

namespace Promotora.WebApi.Controllers;

[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ImagenesController(ICommandHandler<SubirImagenCommand, ImagenSubidaDto> subir,
    IQueryHandler<ObtenerImagenQuery, ImagenContenidoDto> obtener) : ApiControllerBase
{
    [HttpPost("~/api/v1/recintos/{id:int}/imagen-plano")]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [EndpointSummary("Subir plano PNG/JPEG; dueño del recinto o administrador, recurso activo")]
    [ProducesResponseType<ImagenSubidaDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ImagenSubidaDto>> Plano(int id, [FromForm] ImagenUploadRequest request, CancellationToken ct) => SubirAsync(DestinoImagen.Recinto, id, request, ct);

    [HttpPost("~/api/v1/eventos/{id:int}/imagen")]
    [Authorize(Policy = IdentityPolicies.SesionCompleta)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [EndpointSummary("Subir imagen a BORRADOR/PROGRAMADO sin compras; propietario o administrador")]
    [ProducesResponseType<ImagenSubidaDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ImagenSubidaDto>> Evento(int id, [FromForm] ImagenUploadRequest request, CancellationToken ct) => SubirAsync(DestinoImagen.Evento, id, request, ct);

    [HttpPost("~/api/v1/presentadores/{id:int}/imagen")]
    [Authorize(Policy = IdentityPolicies.Administrador)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [EndpointSummary("Subir imagen de presentador activo; administrador")]
    [ProducesResponseType<ImagenSubidaDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<ImagenSubidaDto>> Presentador(int id, [FromForm] ImagenUploadRequest request, CancellationToken ct) => SubirAsync(DestinoImagen.Presentador, id, request, ct);

    private async Task<ActionResult<ImagenSubidaDto>> SubirAsync(DestinoImagen destino, int id, ImagenUploadRequest request, CancellationToken ct)
    {
        await using var stream = request.Archivo.OpenReadStream();
        return FromResult(await subir.HandleAsync(new SubirImagenCommand(destino, id, stream, request.Archivo.Length, request.Archivo.ContentType), ct));
    }

    [HttpGet("{nombre}")]
    [AllowAnonymous]
    [EndpointSummary("Leer PNG solo si está referenciado por un recurso visible públicamente")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImagenContenidoDto>> Obtener(string nombre, CancellationToken ct)
    {
        var result = await obtener.HandleAsync(new ObtenerImagenQuery(nombre), ct);
        if (result.IsFailure) return FromResult(result);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return File(result.Value.Contenido, "image/png");
    }
}

public sealed class ImagenUploadRequest
{
    [FromForm(Name = "archivo")]
    public required IFormFile Archivo { get; init; }
}
