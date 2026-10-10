using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Finance;
using Promotora.Application.Ticketing;
using Promotora.Domain.Finance;

namespace Promotora.Infrastructure.Finance;

public static class FinanzasRegistration
{
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
