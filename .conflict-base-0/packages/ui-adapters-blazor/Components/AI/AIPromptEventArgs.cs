namespace Sunfish.UIAdapters.Blazor.Components.AI;

/// <summary>
/// Cancellable payload fired by <see cref="SunfishAIPrompt.OnPrompt"/>. The handler
/// receives the submitted <see cref="Prompt"/> and may set <see cref="IsCancelled"/>
/// to suppress the default behaviour (clearing the input / appending to history).
/// </summary>
public sealed class AIPromptEventArgs
{
    /// <summary>The prompt text entered by the user.</summary>
    public string Prompt { get; }

    /// <summary>Whether the event handler has cancelled the default behaviour.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Optional model id selected at submission time (null when no picker).</summary>
    public string? SelectedModel { get; }

    /// <summary>Create a new <see cref="AIPromptEventArgs"/>.</summary>
    public AIPromptEventArgs(string prompt, string? selectedModel = null)
    {
        Prompt = prompt;
        SelectedModel = selectedModel;
    }
}

/// <summary>
/// Cancellable payload fired by <see cref="SunfishInlineAIPrompt.OnInline"/>.
/// </summary>
public sealed class InlineAIPromptEventArgs
{
    /// <summary>The prompt text (free-form instruction or selected suggestion).</summary>
    public string Prompt { get; }

    /// <summary>Whether the event handler has cancelled the default behaviour.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Create a new <see cref="InlineAIPromptEventArgs"/>.</summary>
    public InlineAIPromptEventArgs(string prompt) => Prompt = prompt;
}

/// <summary>
/// Cancellable payload fired by <see cref="SunfishPromptBox.OnSubmit"/>.
/// </summary>
public sealed class PromptBoxSubmitEventArgs
{
    /// <summary>The prompt text the user pressed Enter on / clicked submit for.</summary>
    public string Prompt { get; }

    /// <summary>Whether the event handler has cancelled the default behaviour.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Create a new <see cref="PromptBoxSubmitEventArgs"/>.</summary>
    public PromptBoxSubmitEventArgs(string prompt) => Prompt = prompt;
}
