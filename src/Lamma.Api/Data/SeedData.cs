using Lamma.Api.Features.Rides.Geography;
using Lamma.Api.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Api.Data;

public static class SeedData
{
    public static Guid HostId => AccountId(1);
    public static Guid UnapprovedHostId => AccountId(2);
    public static Guid AccountId(int suffix) => Guid.Parse($"10000000-0000-0000-0000-{suffix:000000000000}");
    public static Guid NodeId(int suffix) => Guid.Parse($"20000000-0000-0000-0000-{suffix:000000000000}");

    // Called explicitly by development tooling, never automatically during requests.
    public static async Task ApplyAsync(AppDbContext db, CancellationToken ct = default)
    {
        var accountIds = await db.Users.Select(x => x.Id).ToListAsync(ct);
        foreach (var i in new[] { 1, 2, 11, 12, 13, 14, 15 })
        {
            if (accountIds.Contains(AccountId(i))) continue;
            db.Users.Add(new User { Id = AccountId(i), DisplayName = i switch
                { 1 => "Synthetic approved Host", 2 => "Synthetic unapproved Host", _ => $"Synthetic Rider {i - 10}" },
                IsActive = i != 15, MembershipActive = true, HostApproved = i == 1 });
        }
        var nodeIds = await db.MeetingNodes.Select(x => x.Id).ToListAsync(ct);
        var labels = new[] { "Community gate", "Library meeting point", "Station meeting point", "University gate" };
        foreach (var i in Enumerable.Range(1, 9))
        {
            if (nodeIds.Contains(NodeId(i))) continue;
            db.MeetingNodes.Add(new MeetingNode { Id = NodeId(i), Corridor = i == 9 ? "Other-corridor" : "Demo-corridor",
                Direction = i <= 4 || i == 9 ? "University-bound" : "Community-bound",
                RouteOrder = i <= 4 ? i : i == 9 ? 1 : i - 4,
                Label = i == 9 ? "Other corridor gate" : labels[(i - 1) % 4], IsActive = true });
        }
        await db.SaveChangesAsync(ct);
    }
}
