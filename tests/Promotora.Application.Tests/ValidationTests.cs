using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Validation;
using Promotora.Domain.Shared;

namespace Promotora.Application.Tests;

public sealed class ValidationTests
{
    private sealed record Solicitud(int Cantidad) : ICommand<int>, IQuery<int>;
    private sealed class Validator : AbstractValidator<Solicitud>
    {
        public Validator() => RuleFor(s => s.Cantidad).GreaterThan(0);
    }

    private sealed class Handler : ICommandHandler<Solicitud, int>, IQueryHandler<Solicitud, int>
    {
        public int Calls { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public Task<Result<int>> HandleAsync(Solicitud request, CancellationToken ct)
        {
            Calls++;
            ReceivedToken = ct;
            return Task.FromResult(Result<int>.Success(request.Cantidad));
        }
    }

    [Fact]
    public async Task ComandoInvalidoNoEjecutaHandler()
    {
        var handler = new Handler();
        var pipeline = new ValidatingCommandHandler<Solicitud, int>(handler, [new Validator()]);
        var result = await pipeline.HandleAsync(new Solicitud(0), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, Assert.Single(result.Errors).Type);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task QueryInvalidaNoEjecutaHandler()
    {
        var handler = new Handler();
        var pipeline = new ValidatingQueryHandler<Solicitud, int>(handler, [new Validator()]);
        var result = await pipeline.HandleAsync(new Solicitud(-1), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task SolicitudValidaEjecutaUnaVezYPropagaToken()
    {
        var handler = new Handler();
        var pipeline = new ValidatingCommandHandler<Solicitud, int>(handler, [new Validator()]);
        var ct = TestContext.Current.CancellationToken;
        var result = await pipeline.HandleAsync(new Solicitud(2), ct);
        Assert.Equal(2, result.Value);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(ct, handler.ReceivedToken);
    }

    [Fact]
    public async Task CancelacionNoEjecutaHandler()
    {
        var handler = new Handler();
        var pipeline = new ValidatingCommandHandler<Solicitud, int>(handler, []);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.HandleAsync(new Solicitud(2), cts.Token));
        Assert.Equal(0, handler.Calls);
    }
}
