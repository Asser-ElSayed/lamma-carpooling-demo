using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Lamma.Api.Common.ErrorHandling;
using Lamma.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lamma.Api.Swagger;

public static class ReviewerAccess
{
    private static readonly Dictionary<string, Guid> Accounts = new(StringComparer.Ordinal)
    {
        ["Host"] = SeedData.HostId,
        ["Rider1"] = SeedData.AccountId(11),
        ["Rider2"] = SeedData.AccountId(12),
        ["Rider3"] = SeedData.AccountId(13),
        ["Rider4"] = SeedData.AccountId(14),
        ["UnapprovedHost"] = SeedData.UnapprovedHostId,
        ["InactiveRider"] = SeedData.AccountId(15)
    };

    public static bool MapReviewerAccess(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("ReviewerAccess:Enabled"))
            return false;

        // Documentation tooling only: never part of the three business operations.
        app.MapGet("/swagger/reviewer-access.js", () => Results.Stream(
            typeof(ReviewerAccess).Assembly.GetManifestResourceStream("Lamma.Api.Swagger.reviewer-access.js")!,
            "text/javascript; charset=utf-8")).AllowAnonymous().ExcludeFromDescription();

        app.MapPost("/swagger/reviewer-token", IssueTokenAsync).AllowAnonymous().ExcludeFromDescription();
        return true;
    }

    private static async Task<IResult> IssueTokenAsync(ReviewerAccountRequest request, AppDbContext db,
        IOptionsMonitor<JwtBearerOptions> authentication, TimeProvider clock, HttpContext context, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (!Accounts.TryGetValue(request.Account ?? "", out var accountId))
            return ApiResults.Problem(context, 400, "VALIDATION_FAILED", "Select one of the seven synthetic reviewer accounts.");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == accountId, ct))
            return ApiResults.Problem(context, 503, "REVIEWER_ACCESS_UNAVAILABLE", "The demo owner must initialize the synthetic fixtures.");

        // Reuse the configured developer signing key; never return or embed that key.
        var validation = authentication.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var key = validation.IssuerSigningKey ?? validation.IssuerSigningKeys?.FirstOrDefault();
        var issuer = validation.ValidIssuer ?? validation.ValidIssuers?.FirstOrDefault();
        var audience = validation.ValidAudience ?? validation.ValidAudiences?.FirstOrDefault();
        if (key is not SymmetricSecurityKey || key.KeySize < 256 || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            return ApiResults.Problem(context, 503, "REVIEWER_ACCESS_UNAVAILABLE", "The demo owner must configure the development JWT signing key.");

        var now = clock.GetUtcNow();
        var expires = now.AddHours(1);
        var jwt = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString("D")),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
             new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)],
            now.UtcDateTime.AddSeconds(-5), expires.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Results.Json(new ReviewerTokenResponse(request.Account!, new JwtSecurityTokenHandler().WriteToken(jwt), expires));
    }
}

public sealed record ReviewerAccountRequest([property: JsonRequired] string? Account);
public sealed record ReviewerTokenResponse(string Account, string AccessToken, DateTimeOffset ExpiresAtUtc);
