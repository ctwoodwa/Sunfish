namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// The verb of an authorization request. Common actions are constants; callers
/// may pass arbitrary strings for domain-specific actions (e.g., <c>sign_inspection</c>).
/// </summary>
public readonly record struct CapabilityAction(string Name)
{
    public static readonly CapabilityAction Read     = new("read");
    public static readonly CapabilityAction Write    = new("write");
    public static readonly CapabilityAction Delete   = new("delete");
    public static readonly CapabilityAction Delegate = new("delegate");
    public static readonly CapabilityAction Sign     = new("sign");

    public override string ToString() => Name;
}
