using ClaimTrack.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Controllers;

// The one place a service Result becomes an HTTP response. Controllers call these
// instead of choosing status codes themselves, and Result stays free of HTTP.
public static class ResultExtensions
{
    // Success returns 200 OK with the value.
    public static ActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        return result.ToActionResult(controller, value => controller.Ok(value));
    }

    // Success uses the given response, for example 201 Created.
    public static ActionResult ToActionResult<T>(
        this Result<T> result, ControllerBase controller, Func<T, ActionResult> onSuccess)
    {
        return result.IsSuccess
            ? onSuccess(result.Value!)
            : ToProblem(controller, result.Status, result.Error);
    }

    // Success returns 204 No Content.
    public static ActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        return result.IsSuccess
            ? controller.NoContent()
            : ToProblem(controller, result.Status, result.Error);
    }

    private static ActionResult ToProblem(ControllerBase controller, ResultStatus status, string? error)
    {
        var statusCode = status switch
        {
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            ResultStatus.Invalid => StatusCodes.Status422UnprocessableEntity,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => throw new InvalidOperationException($"No HTTP mapping for failed result status {status}.")
        };

        return controller.Problem(statusCode: statusCode, detail: error);
    }
}
