namespace MyMind.Application.Common;

public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,

    /// <summary>The client's copy is stale (If-Match did not match).</summary>
    PreconditionFailed,

    /// <summary>The request must carry If-Match.</summary>
    PreconditionRequired,
    Forbidden,
}

/// <param name="Field">The request property the error belongs to (camelCase), if any.</param>
public sealed record Error(ErrorKind Kind, string Message, string? Field = null)
{
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);

    public static Error Validation(string message, string? field = null) => new(ErrorKind.Validation, message, field);

    public static Error Conflict(string message, string? field = null) => new(ErrorKind.Conflict, message, field);
}

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result failed: {Error!.Message}");

    /// <summary>For interface-typed values, which C# does not convert implicitly.</summary>
    public static Result<T> From(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(Error error) => new(default, error);
}

/// <summary>For operations that return nothing on success.</summary>
public readonly record struct Success;
