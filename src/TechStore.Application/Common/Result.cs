namespace TechStore.Application.Common;

public static class ResultCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string InsufficientStock = "insufficient_stock";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string NoChange = "no_change";
}

public class Result
{
    protected Result(bool isSuccess, string? error, string? code = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Code = code;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public string? Code { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(string error, string? code = ResultCodes.Validation) => new(false, error, code);
    public static Result NotFound(string error) => Failure(error, ResultCodes.NotFound);
    public static Result Conflict(string error, string? code = ResultCodes.Conflict) => Failure(error, code);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, string? code = ResultCodes.Validation) => Result<T>.Failure(error, code);
    public static Result<T> NotFound<T>(string error) => Result<T>.Failure(error, ResultCodes.NotFound);
    public static Result<T> Conflict<T>(string error, string? code = ResultCodes.Conflict) => Result<T>.Failure(error, code);
}

public class Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? error, string? code = null)
        : base(isSuccess, error, code)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public new static Result<T> Failure(string error, string? code = ResultCodes.Validation) => new(false, default, error, code);
}
