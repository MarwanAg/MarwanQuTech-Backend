using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MarwanQuTech.WebApi.Common;

/// <summary>
/// Adds the "Authorize" button to Swagger UI (paste the accessToken from /api/auth/login) and marks
/// only the endpoints that actually require a token with the lock icon.
/// </summary>
internal sealed partial class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider schemes) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var all = await schemes.GetAllSchemesAsync();
        if (all.All(s => s.Name != "Bearer"))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
                Description = "Access token from POST /api/auth/login"
            }
        };

        // "GET api/admin/projects/{id}" for every action with [Authorize] and without [AllowAnonymous].
        var secured = context.DescriptionGroups
            .SelectMany(group => group.Items)
            .Where(RequiresAuthorization)
            .Select(d => Key(d.HttpMethod, d.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, item) in document.Paths)
        {
            if (item.Operations is null)
                continue;

            foreach (var (method, operation) in item.Operations)
            {
                if (!secured.Contains(Key(method.ToString(), path)))
                    continue;

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            }
        }
    }

    private static bool RequiresAuthorization(ApiDescription description)
    {
        var metadata = description.ActionDescriptor.EndpointMetadata;
        return metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any()
               && !metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
    }

    /// <summary>Same key for "api/x/{id:int}" (ApiExplorer) and "/api/x/{id}" (OpenAPI paths).</summary>
    private static string Key(string? method, string? path)
        => $"{method?.ToUpperInvariant()} {RouteConstraint().Replace((path ?? string.Empty).Trim('/'), "{$1}")}";

    [GeneratedRegex(@"\{([^}:=?]+)[^}]*\}")]
    private static partial Regex RouteConstraint();
}
