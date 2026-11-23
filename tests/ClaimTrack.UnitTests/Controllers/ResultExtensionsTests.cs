using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimTrack.UnitTests.Controllers;

public class ResultExtensionsTests
{
    private readonly ControllerBase _controller = CreateController();

    // ControllerBase.Problem(...) needs MVC's services to build a Problem Details body,
    // so the test controller gets a minimal HTTP context with those services.
    private static ControllerBase CreateController()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers().Services
            .BuildServiceProvider();

        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
    }

    private sealed class TestController : ControllerBase;

    [Fact]
    public void ToActionResult_OkWithValue_Returns200WithValue()
    {
        var actionResult = Result<string>.Ok("policy").ToActionResult(_controller);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be("policy");
    }

    [Fact]
    public void ToActionResult_OkWithCustomSuccess_UsesCustomResponse()
    {
        var actionResult = Result<string>.Ok("claim")
            .ToActionResult(_controller, value => new CreatedResult("/api/claims/1", value));

        var created = actionResult.Should().BeOfType<CreatedResult>().Subject;
        created.Value.Should().Be("claim");
    }

    [Fact]
    public void ToActionResult_OkWithoutValue_Returns204()
    {
        var actionResult = Result.Ok().ToActionResult(_controller);

        actionResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void ToActionResult_NotFound_Returns404ProblemDetails()
    {
        var actionResult = Result<string>.NotFound().ToActionResult(_controller);

        AssertProblem(actionResult, StatusCodes.Status404NotFound, expectedDetail: null);
    }

    [Fact]
    public void ToActionResult_Conflict_Returns409ProblemDetailsWithError()
    {
        var actionResult = Result<string>.Conflict("Claim is under review.").ToActionResult(_controller);

        AssertProblem(actionResult, StatusCodes.Status409Conflict, "Claim is under review.");
    }

    [Fact]
    public void ToActionResult_Invalid_Returns422ProblemDetailsWithError()
    {
        var actionResult = Result<string>.Invalid("Policy not found.").ToActionResult(_controller);

        AssertProblem(actionResult, StatusCodes.Status422UnprocessableEntity, "Policy not found.");
    }

    [Fact]
    public void ToActionResult_Unauthorized_Returns401ProblemDetailsWithError()
    {
        var actionResult = Result<string>.Unauthorized("Invalid email or password.").ToActionResult(_controller);

        AssertProblem(actionResult, StatusCodes.Status401Unauthorized, "Invalid email or password.");
    }

    [Fact]
    public void ToActionResult_FailedResultWithoutValue_ReturnsProblemDetails()
    {
        var actionResult = Result.Conflict("Policy has claims.").ToActionResult(_controller);

        AssertProblem(actionResult, StatusCodes.Status409Conflict, "Policy has claims.");
    }

    private static void AssertProblem(ActionResult actionResult, int expectedStatus, string? expectedDetail)
    {
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);

        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(expectedStatus);
        problem.Detail.Should().Be(expectedDetail);
    }
}
