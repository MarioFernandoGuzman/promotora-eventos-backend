using Promotora.Domain.Shared;

namespace Promotora.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void ExitoContieneValorSinErrores()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void FalloConservaCopiaInmutableYNoPermiteLeerValor()
    {
        var error = new Error("agotado", "Sin cupo", ErrorType.Conflict);
        var source = new[] { error };
        var result = Result<int>.Failure(source);
        source[0] = new Error("otro", "Otro", ErrorType.BusinessRule);
        Assert.True(result.IsFailure);
        Assert.Equal(error, Assert.Single(result.Errors));
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void RechazaResultadosInconsistentes()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
        Assert.Throws<ArgumentException>(() => Result<int>.Failure(Array.Empty<Error>()));
        Assert.Throws<ArgumentException>(() => Result<int>.Failure([null!]));
        Assert.Throws<ArgumentException>(() => new Error("", "Mensaje", ErrorType.Validation));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Error("codigo", "Mensaje", (ErrorType)999));
    }
}
