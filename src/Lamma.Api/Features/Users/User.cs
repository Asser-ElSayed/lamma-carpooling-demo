namespace Lamma.Api.Features.Users;

public sealed class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public bool MembershipActive { get; set; }
    public bool HostApproved { get; set; }
    public bool CanRide => IsActive && MembershipActive;
    public bool CanHost => CanRide && HostApproved;
}
