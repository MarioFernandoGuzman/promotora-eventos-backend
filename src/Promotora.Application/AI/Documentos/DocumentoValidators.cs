using FluentValidation;

namespace Promotora.Application.AI.Documentos;

public sealed class SubirDocumentoValidator : AbstractValidator<SubirDocumentoCommand>
{
    public SubirDocumentoValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200).Must(t => t is not null && !t.Any(char.IsControl));
        RuleFor(x => x.NombreArchivo).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Longitud).InclusiveBetween(1, DocumentoLimites.MaxBytes);
        RuleFor(x => x.Archivo).NotNull();
        RuleFor(x => x.DocumentoAnteriorId).GreaterThan(0).When(x => x.DocumentoAnteriorId.HasValue);
    }
}
public sealed class DocumentosValidator : AbstractValidator<DocumentosQuery>
{
    public DocumentosValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Estado).Must(DocumentoLimites.EstadoValido).When(x => x.Estado is not null);
    }
}
public sealed class DocumentoValidator : AbstractValidator<DocumentoQuery>
{
    public DocumentoValidator() => RuleFor(x => x.Id).GreaterThan(0);
}
public sealed class DesactivarDocumentoValidator : AbstractValidator<DesactivarDocumentoCommand>
{
    public DesactivarDocumentoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EstadoEsperado).Must(DocumentoLimites.EstadoValido);
    }
}
