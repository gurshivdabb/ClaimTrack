using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClaimTrack.Api.Validators;

// Runs before every controller action. For each request object that has a validator,
// it validates it and returns 400 with all errors if it fails, so controllers never
// contain validation code. A new validator is picked up automatically.
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                continue;
            }

            // Same 400 Problem Details shape, with camelCase field names, as every other 400.
            context.Result = ValidationProblems.Create(
                context.HttpContext, result.Errors.Select(error => (error.PropertyName, error.ErrorMessage)));
            return;
        }

        await next();
    }
}
