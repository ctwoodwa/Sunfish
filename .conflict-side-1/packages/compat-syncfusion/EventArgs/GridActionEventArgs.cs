namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped grid-lifecycle begin event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.ActionEventArgs</c> / <c>ActionBeginArgs</c>.
///
/// <para><b>Divergence:</b> Syncfusion publishes a single args type for the <c>ActionBegin</c>
/// event with a string <c>RequestType</c> (paging, sorting, filtering, beginEdit, etc.). The
/// shim preserves that shape; handlers inspect <c>RequestType</c> to disambiguate.</para>
/// </summary>
public class ActionBeginArgs
{
    /// <summary>The action request type (e.g. <c>"paging"</c>, <c>"sorting"</c>, <c>"filtering"</c>).</summary>
    public string? RequestType { get; init; }

    /// <summary>Cancel the action.</summary>
    public bool Cancel { get; set; }

    /// <summary>Optional action-specific payload (Syncfusion's shape is weakly-typed).</summary>
    public object? Data { get; init; }
}

/// <summary>
/// Syncfusion-shaped grid-lifecycle complete event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.ActionCompleteArgs</c>.
/// </summary>
public class ActionCompleteArgs
{
    /// <summary>The action request type.</summary>
    public string? RequestType { get; init; }

    /// <summary>Optional action-specific payload.</summary>
    public object? Data { get; init; }
}

/// <summary>
/// Syncfusion-shaped grid-action failure event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.ActionFailureArgs</c>.
/// </summary>
public class ActionFailureArgs
{
    /// <summary>The action request type.</summary>
    public string? RequestType { get; init; }

    /// <summary>The exception or failure message.</summary>
    public object? Error { get; init; }
}
