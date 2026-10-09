using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Validation;

namespace Promotora.Infrastructure.Shared;

public static class HandlerRegistration
{
    public static IServiceCollection AddCommandHandler<TCommand, TResponse, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResponse>>(provider => new ValidatingCommandHandler<TCommand, TResponse>(
            provider.GetRequiredService<THandler>(), provider.GetServices<IValidator<TCommand>>()));
        return services;
    }

    public static IServiceCollection AddQueryHandler<TQuery, TResponse, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IQueryHandler<TQuery, TResponse>>(provider => new ValidatingQueryHandler<TQuery, TResponse>(
            provider.GetRequiredService<THandler>(), provider.GetServices<IValidator<TQuery>>()));
        return services;
    }
}
