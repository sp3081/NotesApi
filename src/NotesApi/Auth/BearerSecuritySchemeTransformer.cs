using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NotesApi.Auth;

// Adds a "Bearer" security scheme to the OpenAPI document so Swagger UI
// shows an "Authorize" button where you can paste the access token.
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document,
                               OpenApiDocumentTransformerContext context,
                               CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the accessToken returned by POST /api/token."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    }
}
