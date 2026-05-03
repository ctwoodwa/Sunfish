using Sunfish.Foundation.Models;

namespace Sunfish.Foundation.Services;

public interface ISunfishDialogService
{
    Task<bool> ShowConfirmAsync(string title, string message);
    Task ShowAlertAsync(string title, string message);
    Task<string?> ShowPromptAsync(string title, string message, string? defaultValue = null);
    Task<DialogResult> ShowAsync(DialogOptions options);
}

/// <summary>
/// Represents the result of a dialog interaction.
/// </summary>
public enum DialogResult
{
    /// <summary>The dialog was closed without a definitive action.</summary>
    None,

    /// <summary>The user clicked OK or confirmed.</summary>
    Ok,

    /// <summary>The user clicked Cancel.</summary>
    Cancel,

    /// <summary>The user clicked Yes.</summary>
    Yes,

    /// <summary>The user clicked No.</summary>
    No,

    /// <summary>The user clicked Retry.</summary>
    Retry
}
