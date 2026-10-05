using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SkillBridge.Api.Swagger;

public sealed class AuthorizationDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var apiDescription in context.ApiDescriptions)
        {
            if (!apiDescription.TryGetMethodInfo(out var methodInfo))
                continue;
            var attributes = methodInfo.GetCustomAttributes(true)
                .Concat(methodInfo.DeclaringType?.GetCustomAttributes(true) ?? []);
            if (attributes.OfType<IAllowAnonymous>().Any() || !attributes.OfType<IAuthorizeData>().Any())
                continue;

            var path = "/" + apiDescription.RelativePath?.Split('?')[0];
            if (!document.Paths.TryGetValue(path, out var pathItem))
                continue;
            var operation = pathItem.Operations?.FirstOrDefault(pair =>
                pair.Key.ToString().Equals(apiDescription.HttpMethod, StringComparison.OrdinalIgnoreCase)).Value;
            if (operation is null)
                continue;

            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                }
            ];
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Please sign in to continue." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Your account type cannot do this." });
        }
    }
}
