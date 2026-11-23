namespace ClaimTrack.Api.Common;

// The outcome of a service call that returns a value when it succeeds.
public class Result<T>
{
    private Result(ResultStatus status, T? value, string? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value, null);
    public static Result<T> NotFound() => new(ResultStatus.NotFound, default, null);
    public static Result<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
    public static Result<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
    public static Result<T> Unauthorized(string error) => new(ResultStatus.Unauthorized, default, error);
}
