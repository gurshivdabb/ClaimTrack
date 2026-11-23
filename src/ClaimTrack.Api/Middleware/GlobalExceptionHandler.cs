using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Middleware;

// Catches any exception nothing else handled. The full error goes to the log; the client
// only gets a generic 500 Problem Details with a traceId to match it to the log entry.
// Expected failures (not found, conflict) never get here: services return them as Results.
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Same id the Problem Details response shows as "traceId".
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        logger.LogError(exception,
            "Unhandled exception for {Method} {Path} (traceId {TraceId})",
            httpContext.Request.Method, httpContext.Request.Path, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Never include the exception message or stack trace in the response.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        });
    }
}
