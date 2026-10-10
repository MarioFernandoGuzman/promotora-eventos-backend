using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Catalog;

public static class CatalogRegistration
{
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddScoped<CatalogAccess>();
        services.AddScoped<ICatalogTransaction, EfCatalogTransaction>();
        services.AddScoped<ICategoriaEventoRepository, EfCategoriaEventoRepository>();
        services.AddScoped<IValidator<CrearCategoriaEventoCommand>, CrearCategoriaEventoValidator>();
        services.AddScoped<IValidator<ActualizarCategoriaEventoCommand>, ActualizarCategoriaEventoValidator>();
        services.AddScoped<IValidator<ListarCategoriaEventoQuery>, ListarCategoriaEventoValidator>();
        services.AddCommandHandler<CrearCategoriaEventoCommand, CategoriaEventoDto, CrearCategoriaEventoHandler>();
        services.AddCommandHandler<ActualizarCategoriaEventoCommand, CategoriaEventoDto, ActualizarCategoriaEventoHandler>();
        services.AddCommandHandler<BajaCategoriaEventoCommand, Unit, BajaCategoriaEventoHandler>();
        services.AddQueryHandler<ObtenerCategoriaEventoQuery, CategoriaEventoDto, ObtenerCategoriaEventoHandler>();
        services.AddQueryHandler<ListarCategoriaEventoQuery, PaginaDto<CategoriaEventoDto>, ListarCategoriaEventoHandler>();
        services.AddScoped<IPresentadorRepository, EfPresentadorRepository>();
        services.AddScoped<IValidator<CrearPresentadorCommand>, CrearPresentadorValidator>();
        services.AddScoped<IValidator<ActualizarPresentadorCommand>, ActualizarPresentadorValidator>();
        services.AddScoped<IValidator<ListarPresentadorQuery>, ListarPresentadorValidator>();
        services.AddCommandHandler<CrearPresentadorCommand, PresentadorDto, CrearPresentadorHandler>();
        services.AddCommandHandler<ActualizarPresentadorCommand, PresentadorDto, ActualizarPresentadorHandler>();
        services.AddCommandHandler<BajaPresentadorCommand, Unit, BajaPresentadorHandler>();
        services.AddQueryHandler<ObtenerPresentadorQuery, PresentadorDto, ObtenerPresentadorHandler>();
        services.AddQueryHandler<ListarPresentadorQuery, PaginaDto<PresentadorDto>, ListarPresentadorHandler>();
        return services;
    }
}
