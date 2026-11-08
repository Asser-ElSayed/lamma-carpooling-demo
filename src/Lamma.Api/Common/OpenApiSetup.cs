using Microsoft.OpenApi;
using System.Text.Json.Nodes;
using Lamma.Api.Data;

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
            {
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
                if (operation.OperationId == "PublishRide" && operation.RequestBody?.Content?["application/json"] is OpenApiMediaType media)
                    media.Example = new JsonObject
                    {
                        ["originNodeId"] = SeedData.NodeId(1).ToString(),
                        ["destinationNodeId"] = SeedData.NodeId(4).ToString(),
                        ["departureAtUtc"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                        ["allowsIntermediateStops"] = true
                    };
                if (operation.Parameters?.SingleOrDefault(p => p.Name == "Idempotency-Key") is OpenApiParameter key)
                {
                    key.Description = "Fresh UUID per logical attempt; reuse for retries. Rider-scoped, case-sensitive, 1–128 characters without surrounding whitespace.";
                    key.Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 128 };
                }
            }
            if (document.Components.Schemas?.TryGetValue("ProblemDetails", out var problemSchema) == true && problemSchema is OpenApiSchema problem)
            {
                problem.Properties ??= new Dictionary<string, IOpenApiSchema>();
                problem.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable application error code." };
                problem.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Safe request correlation identifier." };
            }
            return Task.CompletedTask;
        }));
        return services;
    }
}
