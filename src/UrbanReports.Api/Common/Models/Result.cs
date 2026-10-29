namespace UrbanReports.Api.Common;

public readonly struct Result<T>
{
    private Result(T? value, ApplicationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public ApplicationError? Error { get; }

    public bool Success => Error is null;

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(ApplicationError error) => new(default, error);
}

public readonly record struct Empty;
