using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Services;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

// Bad paging or sorting values are rejected with 400, not quietly corrected,
// so the client always gets exactly what it asked for or a clear reason why not.
public class ClaimListQueryValidator : AbstractValidator<ClaimListQuery>
{
    public ClaimListQueryValidator()
    {
        // The number of rows to skip, (page - 1) × pageSize, must fit in an int. A huge page number
        // would otherwise overflow to a negative offset and make the database call throw (a 500).
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .Must((query, page) => (long)(page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("Page is too large.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, ClaimListQuery.MaxPageSize);

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Sort)
            .Must(ClaimSorting.IsValid)
            .WithMessage($"Sort must be one of: {string.Join(", ", ClaimSorting.Fields)}, optionally starting with '-' for descending.");
    }
}
