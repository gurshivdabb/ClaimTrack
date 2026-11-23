namespace ClaimTrack.Api.Common;

// The outcome of a service call that returns no value, such as a delete.
// Expected failures (not found, conflict) are returned here instead of thrown as exceptions.
public class Result
{
    private Result(ResultStatus status, string? error)
    {
        Status = status;
        Error = error;
    }

    public ResultStatus Status { get; }
    public string? Error { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result Ok() => new(ResultStatus.Ok, null);
    public static Result NotFound() => new(ResultStatus.NotFound, null);
    public static Result Conflict(string error) => new(ResultStatus.Conflict, error);
}
