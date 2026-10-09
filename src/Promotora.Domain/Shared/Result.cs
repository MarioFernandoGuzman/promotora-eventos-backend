namespace Promotora.Domain.Shared;

public sealed class Result<T>
{
    private readonly T? _value;
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Un resultado fallido no contiene valor.");

    private Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        IsSuccess = true;
        Errors = Array.Empty<Error>();
    }

    private Result(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var copy = errors.ToArray();
        if (copy.Length == 0 || copy.Any(e => e is null))
        {
            throw new ArgumentException("Un fallo requiere al menos un error válido.", nameof(errors));
        }

        Errors = Array.AsReadOnly(copy);
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(params Error[] errors) => new((IEnumerable<Error>)errors);
    public static Result<T> Failure(IEnumerable<Error> errors) => new(errors);
}
