using AwesomeAssertions;
using ClaimTrack.Api.Common;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ClaimTrack.UnitTests.Common;

public class BearerSecuritySchemeTransformerTests
{
    private static async Task<OpenApiDocument> TransformEmptyDocumentAsync()
    {
        var document = new OpenApiDocument();
        var context = new OpenApiDocumentTransformerContext
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await new BearerSecuritySchemeTransformer().TransformAsync(document, context, CancellationToken.None);

        return document;
    }

    [Fact]
    public async Task TransformAsync_EmptyDocument_AddsBearerJwtScheme()
    {
        var document = await TransformEmptyDocumentAsync();

        var scheme = document.Components!.SecuritySchemes!["Bearer"];
        scheme.Type.Should().Be(SecuritySchemeType.Http);
        scheme.Scheme.Should().Be("bearer");
        scheme.BearerFormat.Should().Be("JWT");
    }

    [Fact]
    public async Task TransformAsync_EmptyDocument_RequiresTheSchemeForTheWholeApi()
    {
        var document = await TransformEmptyDocumentAsync();

        var requirement = document.Security.Should().ContainSingle().Subject;
        requirement.Keys.Should().ContainSingle()
            .Which.Reference.Id.Should().Be("Bearer");
    }
}
