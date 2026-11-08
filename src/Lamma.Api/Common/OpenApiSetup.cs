using Microsoft.OpenApi;

namespace Lamma.Api.Common;

public static class OpenApiSetup
{
    public static IServiceCollection AddDemoOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new() { Title = "Lamma — Credosis Backend Demo", Version = "v1",
                Description = "Publish a three-seat ride, search a curated corridor, and confirm one seat atomically. Use signed development JWTs via Authorize. Search availability is a snapshot." };
            document.Components ??= new();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http,
                    Scheme = "bearer", BearerFormat = "JWT", Description = "Paste a development JWT only; Swagger adds Bearer." }
            };
            foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations!.Values))
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }));
        return services;
    }
}
