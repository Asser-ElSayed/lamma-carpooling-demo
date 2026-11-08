using System.Text.Json.Serialization;
using Lamma.Api.Common;
using Lamma.Api.Common.ErrorHandling;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("This synthetic-identity demonstration must run only in Development.");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(5);
    o.TokenValidationParameters.NameClaimType = "sub";
    o.Events = new JwtBearerEvents
    {
        OnChallenge = async c =>
        {
            c.HandleResponse();
            c.Response.Headers.WWWAuthenticate = "Bearer";
            await ApiResults.Problem(c.HttpContext, 401, "UNAUTHENTICATED", "A valid signed bearer token is required.").ExecuteAsync(c.HttpContext);
        },
        OnForbidden = c => ApiResults.Problem(c.HttpContext, 403, "FORBIDDEN", "This action is not permitted.").ExecuteAsync(c.HttpContext)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddDemoOpenApi();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async c =>
    await ApiResults.Problem(c.HttpContext, c.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be completed.").ExecuteAsync(c.HttpContext));
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
    await next(context);
    app.Logger.LogInformation("{Method} {Path} returned {Status}; trace {TraceId}",
        context.Request.Method, context.Request.Path, context.Response.StatusCode, context.TraceIdentifier);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Lamma v1");
    o.DocumentTitle = "Lamma API — Swagger";
    o.EnableTryItOutByDefault();
});
app.Run();

public partial class Program;
