using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Services;

// Sorting runs here on in-memory lists; the integration tests run the same sorts as SQL.
public class ClaimSortingTests
{
    // Ids are created in ascending order so the tie-breaker's direction is easy to see.
    private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ThirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly DateTimeOffset Day1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static IQueryable<Claim> Claims() => new[]
    {
        new Claim { Id = FirstId, ClaimNumber = "CLM-2026-00002", ClaimedAmount = 300m, IncidentDate = new(2026, 5, 3), SubmittedAt = Day1 },
        new Claim { Id = SecondId, ClaimNumber = "CLM-2026-00003", ClaimedAmount = 100m, IncidentDate = new(2026, 5, 1), SubmittedAt = Day1.AddDays(2) },
        new Claim { Id = ThirdId, ClaimNumber = "CLM-2026-00001", ClaimedAmount = 200m, IncidentDate = new(2026, 5, 2), SubmittedAt = Day1.AddDays(1) }
    }.AsQueryable();

    [Theory]
    [InlineData("claimedAmount", new[] { 2, 3, 1 })]
    [InlineData("-claimedAmount", new[] { 1, 3, 2 })]
    [InlineData("incidentDate", new[] { 2, 3, 1 })]
    [InlineData("-incidentDate", new[] { 1, 3, 2 })]
    [InlineData("submittedAt", new[] { 1, 3, 2 })]
    [InlineData("-submittedAt", new[] { 2, 3, 1 })]
    [InlineData("claimNumber", new[] { 3, 1, 2 })]
    [InlineData("-claimNumber", new[] { 2, 1, 3 })]
    [InlineData("CLAIMEDAMOUNT", new[] { 2, 3, 1 })] // any capitalisation
    public void ApplySort_Field_OrdersByIt(string sort, int[] expectedOrder)
    {
        var ids = Claims().ApplySort(sort).Select(c => c.Id).ToList();

        ids.Should().Equal(expectedOrder.Select(n => Guid.Parse($"00000000-0000-0000-0000-00000000000{n}")));
    }

    [Fact]
    public void ApplySort_NoSort_IsNewestFirst()
    {
        Claims().ApplySort(null).Select(c => c.Id).Should().Equal(SecondId, ThirdId, FirstId);
    }

    [Theory]
    [InlineData("claimedAmount", new[] { 1, 2, 3 })]
    [InlineData("-claimedAmount", new[] { 3, 2, 1 })]
    public void ApplySort_EqualValues_TieBreakOnIdInTheSameDirection(string sort, int[] expectedOrder)
    {
        var tied = new[] { ThirdId, FirstId, SecondId }
            .Select(id => new Claim { Id = id, ClaimedAmount = 100m })
            .AsQueryable();

        tied.ApplySort(sort).Select(c => c.Id).Should().Equal(
            expectedOrder.Select(n => Guid.Parse($"00000000-0000-0000-0000-00000000000{n}")));
    }

    [Fact]
    public void ApplySort_UnknownField_Throws()
    {
        var act = () => Claims().ApplySort("description").ToList();

        act.Should().Throw<ArgumentException>();
    }

    // ---- ClaimListQueryValidator ----

    private readonly ClaimListQueryValidator _validator = new();

    [Fact]
    public void Validate_Defaults_HasNoErrors()
    {
        _validator.TestValidate(new ClaimListQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("submittedAt")]
    [InlineData("-claimNumber")]
    [InlineData("IncidentDate")]
    public void Validate_KnownSort_HasNoError(string sort)
    {
        _validator.TestValidate(new ClaimListQuery(Sort: sort)).ShouldNotHaveValidationErrorFor(x => x.Sort);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("--submittedAt")]
    [InlineData("")]
    public void Validate_UnknownSort_HasError(string sort)
    {
        _validator.TestValidate(new ClaimListQuery(Sort: sort)).ShouldHaveValidationErrorFor(x => x.Sort);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageBelowOne_HasError(int page)
    {
        _validator.TestValidate(new ClaimListQuery(Page: page)).ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_PageWhoseOffsetWouldOverflow_HasError()
    {
        // (2147483647 - 1) × 20 doesn't fit in an int.
        _validator.TestValidate(new ClaimListQuery(Page: int.MaxValue)).ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_LargestPageThatFits_HasNoError()
    {
        // (107374183 - 1) × 20 = 2147483640, just under int.MaxValue.
        _validator.TestValidate(new ClaimListQuery(Page: 107_374_183)).ShouldNotHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void Validate_PageSizeBoundaries(int pageSize, bool expectError)
    {
        var result = _validator.TestValidate(new ClaimListQuery(PageSize: pageSize));

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.PageSize);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
        }
    }

    // ---- PagedResult ----

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(41, 20, 3)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expectedPages)
    {
        new PagedResult<int>([], 1, pageSize, totalCount).TotalPages.Should().Be(expectedPages);
    }
}
