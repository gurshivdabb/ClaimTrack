using System.Reflection;
using AwesomeAssertions;
using ClaimTrack.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.UnitTests.Controllers;

// Guards for the future: these fail if anyone changes who may call an endpoint without
// updating the role table from requirements §2 here as well.
public class ControllerSecurityTests
{
    private static readonly IEnumerable<Type> Controllers = typeof(AuthController).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract);

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    // Requirements §2 and §6. null = any logged-in user (visibility, BR-09, decides what they see).
    // A new endpoint must be added here, or the test below fails.
    private static readonly Dictionary<string, string?> RequiredRoles = new()
    {
        ["PoliciesController.GetAll"] = null,
        ["PoliciesController.GetById"] = null,
        ["PoliciesController.Create"] = "Supervisor",
        ["PoliciesController.Update"] = "Supervisor",
        ["PoliciesController.Delete"] = "Supervisor",
        ["ClaimsController.GetAll"] = null,
        ["ClaimsController.GetById"] = null,
        ["ClaimsController.History"] = null,
        ["ClaimsController.Submit"] = "Policyholder",
        ["ClaimsController.Update"] = "Policyholder",
        ["ClaimsController.Assign"] = "Supervisor",          // BR-04
        ["ClaimsController.StartReview"] = "Adjuster",
        ["ClaimsController.Recommend"] = "Adjuster",
        ["ClaimsController.Decide"] = "Supervisor",
        ["ClaimsController.Withdraw"] = "Policyholder",
        ["DocumentsController.Upload"] = null,   // whoever can see the claim (BR-09)
        ["DocumentsController.List"] = null,
        ["DocumentsController.Download"] = null,
        ["UsersController.GetAll"] = "Supervisor"
    };

    [Fact]
    public void EveryProtectedAction_RequiresTheRoleFromTheRequirements()
    {
        var actual = Controllers
            .Where(c => c != typeof(AuthController))
            .SelectMany(c => ActionsOf(c).Select(action => new
            {
                Name = $"{c.Name}.{action.Name}",
                Roles = action.GetCustomAttribute<AuthorizeAttribute>()?.Roles
            }))
            .ToDictionary(a => a.Name, a => a.Roles);

        actual.Should().BeEquivalentTo(RequiredRoles);
    }

    [Fact]
    public void AllowAnonymous_IsOnlyUsedByAuthController()
    {
        var publicPlaces = Controllers
            .SelectMany(controller => ActionsOf(controller)
                .Where(action => action.IsDefined(typeof(AllowAnonymousAttribute)))
                .Select(action => $"{controller.Name}.{action.Name}")
                .Concat(controller.IsDefined(typeof(AllowAnonymousAttribute)) ? [controller.Name] : []));

        publicPlaces.Should().BeEquivalentTo([nameof(AuthController)]);
    }

    [Fact]
    public void ClaimsController_HasNoDeleteEndpoint()
    {
        ActionsOf(typeof(ClaimsController))
            .Where(action => action.IsDefined(typeof(HttpDeleteAttribute)))
            .Should().BeEmpty("claims are never hard-deleted (BR-12)");
    }
}
