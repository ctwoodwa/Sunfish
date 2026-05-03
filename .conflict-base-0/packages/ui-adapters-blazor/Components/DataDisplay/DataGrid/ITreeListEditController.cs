namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Internal interface for toolbar buttons to invoke edit operations on the parent TreeList.
/// </summary>
internal interface ITreeListEditController
{
    /// <summary>Begin adding a new row.</summary>
    Task BeginAddAsync();

    /// <summary>Save the currently editing row.</summary>
    Task SaveEditAsync();

    /// <summary>Cancel the current edit and revert changes.</summary>
    Task CancelEditAsync();
}
