namespace Sunfish.Bridge.Data.Authorization;

public interface ITenantContext
{
    string TenantId { get; }
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}
