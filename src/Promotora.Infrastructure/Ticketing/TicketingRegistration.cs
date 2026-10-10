using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Ticketing;

public static class TicketingRegistration
{
    public static IServiceCollection AddEmisionBoletosBase64(this IServiceCollection services, string? signingKey, int? categoriaCarreraId = null)
    {
        byte[]? bytes = null;
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            try { bytes = Convert.FromBase64String(signingKey); }
            catch (FormatException) { throw new ArgumentException("Boletos:SigningKey debe ser Base64 externo de al menos 32 bytes.", nameof(signingKey)); }
        }
        return services.AddEmisionBoletos(bytes, categoriaCarreraId);
    }

    public static IServiceCollection AddEmisionBoletos(this IServiceCollection services, byte[]? signingKey, int? categoriaCarreraId = null)
    {
        if (categoriaCarreraId is <= 0) throw new ArgumentOutOfRangeException(nameof(categoriaCarreraId));
        services.AddSingleton(new EmisionBoletosOptions(categoriaCarreraId));
        services.AddSingleton<ITicketTokenService>(new HmacTicketTokenService(signingKey));
        services.AddScoped<IEmisionBoletosRepository, EfEmisionBoletosRepository>();
        services.AddScoped<IEmisionBoletosPort, EmisionBoletosService>();
        return services;
    }

    public static IServiceCollection AddTicketing(this IServiceCollection services, CompraOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ReservaMinutos);
        if (options.Moneda != "GTQ") throw new ArgumentException("El catálogo actual utiliza GTQ.", nameof(options));
        services.AddSingleton(options);
        services.AddScoped<ICompraRepository, EfCompraRepository>();
        services.AddScoped<ICompraConsultaRepository, EfCompraConsultaRepository>();
        services.AddScoped<ClienteCompraAccess>();
        services.AddScoped<IValidator<ObtenerCompraQuery>, ObtenerCompraValidator>();
        services.AddScoped<IValidator<MisComprasQuery>, MisComprasValidator>();
        services.AddScoped<IValidator<MisBoletosQuery>, MisBoletosValidator>();
        services.AddScoped<IValidator<CancelarCompraCommand>, CancelarCompraValidator>();
        services.AddQueryHandler<ObtenerCompraQuery, CompraCreadaDto, ObtenerCompraHandler>();
        services.AddQueryHandler<MisComprasQuery, PaginaDto<CompraCreadaDto>, MisComprasHandler>();
        services.AddQueryHandler<MisBoletosQuery, PaginaDto<BoletoClienteDto>, MisBoletosHandler>();
        services.AddCommandHandler<CancelarCompraCommand, CompraCreadaDto, CancelarCompraHandler>();
        services.AddScoped<ICupoEntradaRepository, EfCupoEntradaRepository>();
        services.AddScoped<ITicketingTransaction, EfTicketingTransaction>();
        services.AddScoped<IValidator<CrearCompraCommand>, CrearCompraValidator>();
        services.AddCommandHandler<CrearCompraCommand, CrearCompraResultado, CrearCompraHandler>();
        return services;
    }
}
