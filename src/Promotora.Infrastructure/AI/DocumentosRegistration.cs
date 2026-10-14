using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.AI.Documentos;
using Promotora.Application.Shared;
using Promotora.Infrastructure.AI.Persistence;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.AI;

public static class DocumentosRegistration
{
    public static IServiceCollection AddDocumentosRag(this IServiceCollection services)
    {
        services.AddScoped<IDocumentoRepository, EfDocumentoRepository>();
        services.AddScoped<ITextoDocumentoExtractor, TextoDocumentoExtractor>();
        services.AddScoped<DocumentoAccess>();
        services.AddScoped<IValidator<SubirDocumentoCommand>, SubirDocumentoValidator>();
        services.AddScoped<IValidator<DocumentosQuery>, DocumentosValidator>();
        services.AddScoped<IValidator<DocumentoQuery>, DocumentoValidator>();
        services.AddScoped<IValidator<DesactivarDocumentoCommand>, DesactivarDocumentoValidator>();
        services.AddCommandHandler<SubirDocumentoCommand, DocumentoDto, SubirDocumentoHandler>();
        services.AddQueryHandler<DocumentosQuery, PaginaDto<DocumentoDto>, DocumentosHandler>();
        services.AddQueryHandler<DocumentoQuery, DocumentoDetalleDto, DocumentoHandler>();
        services.AddCommandHandler<DesactivarDocumentoCommand, DocumentoDto, DesactivarDocumentoHandler>();
        return services;
    }
}
