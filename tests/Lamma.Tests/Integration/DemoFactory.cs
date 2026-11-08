using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Lamma.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lamma.Tests.Integration;

// Real signed JWTs and the real SQL provider; authentication is never replaced by a fake handler.
public sealed class DemoFactory(SqlFixture sql, DbCommandInterceptor? interceptor = null, TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Lamma"] = sql.ConnectionString,
            ["Logging:LogLevel:Default"] = "None"
        }));
        builder.ConfigureServices(services =>
        {
            if (clock is not null) services.AddSingleton(clock);
            if (interceptor is not null) services.AddDbContext<AppDbContext>(o => o.AddInterceptors(interceptor));
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.TokenValidationParameters = new()
                {
                    ValidateIssuer = true, ValidIssuer = "lamma-tests", ValidateAudience = true, ValidAudience = "lamma-tests",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero, NameClaimType = "sub"
                };
            });
        });
    }

    public HttpClient As(Guid accountId, DateTime? expires = null, bool wrongKey = false)
    {
        var client = CreateClient();
        var token = new JwtSecurityToken("lamma-tests", "lamma-tests", [new Claim("sub", accountId.ToString())],
            expires: expires ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new(new SymmetricSecurityKey(wrongKey ? RandomNumberGenerator.GetBytes(32) : key), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
