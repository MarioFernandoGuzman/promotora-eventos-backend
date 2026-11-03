using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Reporting;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Reporting;

public static class ReportingRegistration
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddScoped<ReporteAccess>();
        services.AddScoped<IReporteRepository, SqlReporteRepository>();
        services.AddScoped<IValidator<ReporteQuery>, ReporteValidator>();
        services.AddQueryHandler<ReporteQuery, ReporteDto, ReporteHandler>();
        return services;
    }
}
