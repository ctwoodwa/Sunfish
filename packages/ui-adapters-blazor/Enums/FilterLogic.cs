namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Logical operator used to combine a group of child filter expressions in
/// <c>SunfishFilter</c>.
/// </summary>
public enum FilterLogic
{
    /// <summary>All child expressions must match.</summary>
    And,

    /// <summary>Any child expression must match.</summary>
    Or
}
