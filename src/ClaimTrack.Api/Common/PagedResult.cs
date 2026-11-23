namespace ClaimTrack.Api.Common;

// One page of a longer list, plus what a client needs to show "page 2 of 3" or fetch the next page.
public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount)
{
    // Rounds up: 41 items at 20 per page is 3 pages. No items is 0 pages.
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}
