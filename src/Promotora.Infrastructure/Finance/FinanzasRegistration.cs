using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Finance;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Ticketing;
using Promotora.Domain.Finance;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Finance;

public static class FinanzasRegistration
{
    public static IServiceCollection AddReembolsos(this IServiceCollection services)
    {
        services.AddScoped<ICobroRepository, EfCobroRepository>();
        services.AddScoped<IReembolsoRepository, EfReembolsoRepository>();
        services.AddScoped<IValidator<ReembolsarCompraCommand>, ReembolsarCompraValidator>();
        services.AddCommandHandler<ReembolsarCompraCommand, ReembolsoDto, ReembolsarCompraHandler>();
        return services;
    }
    public static IServiceCollection AddPagoSimuladoDurable(this IServiceCollection services, FakePaymentOptions options, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (options.TasaFallo is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(options));
        services.AddSingleton<IPaymentGateway>(sp => new PasarelaSimuladaDurable(connectionString, options, sp.GetRequiredService<Promotora.Application.Shared.Ports.IClock>()));
        return services;
    }
    public static IServiceCollection AddFinalizacionCompras(this IServiceCollection services)
    {
        services.AddScoped<ICobroRepository, EfCobroRepository>();
        services.AddScoped<ICompletarCompraPort, CompletarCompraService>();
        return services;
    }

    public static IServiceCollection AddCuentasFinancieras(this IServiceCollection services)
    {
        services.AddScoped<ICuentaRepository, EfCuentaRepository>();
        services.AddScoped<IValidator<CerrarCuentaCommand>, CerrarCuentaValidator>();
        services.AddCommandHandler<CerrarCuentaCommand, CuentaDto, CerrarCuentaHandler>();
        return services;
    }

    public static IServiceCollection AddPagoSimulado(this IServiceCollection services, FakePaymentOptions options)
    {
        if (options.TasaFallo is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(options));
        services.AddSingleton(options);
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        return services;
    }

    public static IServiceCollection AddFinanzas(this IServiceCollection services, decimal tasaIva, decimal comisionPorcentaje)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tasaIva);
        ArgumentOutOfRangeException.ThrowIfNegative(comisionPorcentaje);
        services.AddSingleton<IPoliticaTarifariaProvider>(new PoliticaTarifariaProvider(new(tasaIva, comisionPorcentaje)));
        services.AddScoped<CrearCompraFactory>();
        return services;
    }
    private sealed class PoliticaTarifariaProvider(PoliticaTarifaria politica) : IPoliticaTarifariaProvider
    {
        public PoliticaTarifaria Obtener() => politica;
    }
}
