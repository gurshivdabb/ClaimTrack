using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Validators;

// The one place a 400 validation response is built, used both by ValidationFilter (our
// FluentValidation rules) and by ASP.NET's automatic 400 for values it can't read at all
// (e.g. ?status=Pending). Field names follow the API's JSON naming policy, so a client sees
// "pageSize" for the parameter it sent as "pageSize".
public static class ValidationProblems
{
    // Shown instead of an internal exception message when ASP.NET gives no message of its own.
    private const string GenericMessage = "The value is not valid.";

    public static BadRequestObjectResult Create(HttpContext httpContext, IEnumerable<(string Field, string Message)> errors)
    {
        var services = httpContext.RequestServices;
        var namingPolicy = services.GetRequiredService<IOptions<JsonOptions>>()
            .Value.JsonSerializerOptions.PropertyNamingPolicy;

        // A fresh dictionary: ASP.NET's own one already holds PascalCase keys from binding, and
        // its keys ignore case, so adding "pageSize" there would land under "PageSize".
        var modelState = new ModelStateDictionary();
        foreach (var (field, message) in errors)
        {
            var jsonName = namingPolicy?.ConvertName(field) ?? field;
            modelState.AddModelError(jsonName, string.IsNullOrWhiteSpace(message) ? GenericMessage : message);
        }

        var problem = services.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(httpContext, modelState);
        return new BadRequestObjectResult(problem);
    }

    // Errors ASP.NET found while reading the request, as (field, message) pairs.
    public static IEnumerable<(string Field, string Message)> FromModelState(ModelStateDictionary modelState) =>
        modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => (entry.Key, error.ErrorMessage)));
}
