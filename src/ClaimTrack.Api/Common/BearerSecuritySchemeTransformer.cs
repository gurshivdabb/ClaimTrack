using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ClaimTrack.Api.Common;

// Edits the generated OpenAPI document so Swagger UI shows an "Authorize" button
// and sends "Authorization: Bearer <token>" with every request once you're logged in.
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = JwtBearerDefaults.AuthenticationScheme; // "Bearer"

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // 1. Describe how tokens are sent.
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the accessToken from POST /api/auth/login."
        };

        // 2. Apply it to the whole API. Register and login don't need a token, but sending one is harmless.
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }
}
