using System.Security.Claims;
using Lamma.Api.Common.ErrorHandling;
using Lamma.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Api.Features.Users;

public static class CurrentUser
{
    public static Guid Id(ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    public static async Task<ApiError?> CheckAsync(AppDbContext db, Guid id, bool host, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || !user.CanRide)
            return new(403, "MEMBERSHIP_INACTIVE", "An active community account is required.");
        return host && !user.CanHost ? new(403, "HOST_NOT_APPROVED", "Current Host approval is required.") : null;
    }
}
