using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.MvpEditor;

/// <summary>
/// MVP rich-text editor using <c>contenteditable</c> + <c>document.execCommand</c>.
///
/// <para>
/// This is the Forms/Inputs canonical MVP surface (ADR 0022, Tier 3 W3-7).
/// It is deliberately distinct from the richer, production editor at
/// <c>Sunfish.UIAdapters.Blazor.Components.Editors.SunfishEditor</c>, which
/// is not bound to the deprecated <c>execCommand</c> API. Both may coexist —
/// this one lives in a nested namespace so the unqualified name
/// <c>SunfishEditor</c> resolves to the production editor when consumers
/// import <c>Sunfish.UIAdapters.Blazor.Components.Editors</c>.
/// </para>
///
/// <para>
/// Known gap: <c>document.execCommand</c> is deprecated but still widely
/// supported. A follow-up should migrate to Selection / Range APIs for
/// formatting.
/// </para>
/// </summary>
public partial class SunfishEditor : SunfishComponentBase
{
    private ElementReference _surfaceRef;

    /// <summary>The HTML string being edited. Supports two-way binding.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Fires on every input event, with the current HTML contents.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Fires on every input event — alias for <see cref="ValueChanged"/>.</summary>
    [Parameter] public EventCallback<string> OnValueChange { get; set; }

    /// <summary>Placeholder rendered when the surface is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>When <c>true</c>, the surface is not editable.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Minimum rendered height (CSS). Default: <c>"200px"</c>.</summary>
    [Parameter] public string MinHeight { get; set; } = "200px";

    /// <summary>
    /// The toolbar items to expose. Defaults to a common set of
    /// Bold/Italic/Underline, list controls, Link, Undo/Redo.
    /// </summary>
    [Parameter]
    public List<EditorToolbarItem> Tools { get; set; } = new()
    {
        EditorToolbarItem.Bold,
        EditorToolbarItem.Italic,
        EditorToolbarItem.Underline,
        EditorToolbarItem.BulletList,
        EditorToolbarItem.NumberedList,
        EditorToolbarItem.Link,
        EditorToolbarItem.Undo,
        EditorToolbarItem.Redo,
    };

    private string SurfaceStyle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(MinHeight)) parts.Add($"min-height:{MinHeight}");
        parts.Add("outline:none");
        parts.Add("padding:0.5rem 0.75rem");
        parts.Add("border:1px solid var(--sunfish-border,#d1d5db)");
        parts.Add("border-radius:0.375rem");
        return string.Join(";", parts);
    }

    private async Task HandleInput(ChangeEventArgs e)
    {
        var html = e.Value?.ToString() ?? string.Empty;
        Value = html;
        await ValueChanged.InvokeAsync(html);
        await OnValueChange.InvokeAsync(html);
    }

    private async Task InvokeToolAsync(EditorToolbarItem tool)
    {
        if (ReadOnly) return;

        try
        {
            // Focus the surface first so the command applies to the right context.
            await _surfaceRef.FocusAsync();
        }
        catch
        {
            // Focus can fail in prerender — proceed and let execCommand retry.
        }

        switch (tool)
        {
            case EditorToolbarItem.Bold:
                await ExecCommand("bold");
                break;
            case EditorToolbarItem.Italic:
                await ExecCommand("italic");
                break;
            case EditorToolbarItem.Underline:
                await ExecCommand("underline");
                break;
            case EditorToolbarItem.BulletList:
                await ExecCommand("insertUnorderedList");
                break;
            case EditorToolbarItem.NumberedList:
                await ExecCommand("insertOrderedList");
                break;
            case EditorToolbarItem.Heading:
                await ExecCommand("formatBlock", "<h2>");
                break;
            case EditorToolbarItem.Link:
            {
                var url = await JS.InvokeAsync<string?>("prompt", "Enter URL", "https://");
                if (!string.IsNullOrWhiteSpace(url)) await ExecCommand("createLink", url);
                break;
            }
            case EditorToolbarItem.Image:
            {
                var src = await JS.InvokeAsync<string?>("prompt", "Image URL", "https://");
                if (!string.IsNullOrWhiteSpace(src)) await ExecCommand("insertImage", src);
                break;
            }
            case EditorToolbarItem.Undo:
                await ExecCommand("undo");
                break;
            case EditorToolbarItem.Redo:
                await ExecCommand("redo");
                break;
            case EditorToolbarItem.FontSize:
            {
                // execCommand fontSize takes 1–7 (legacy)
                var size = await JS.InvokeAsync<string?>("prompt", "Font size (1-7)", "3");
                if (!string.IsNullOrWhiteSpace(size)) await ExecCommand("fontSize", size);
                break;
            }
            case EditorToolbarItem.Color:
            {
                var color = await JS.InvokeAsync<string?>("prompt", "Color (e.g. #ff0000)", "#000000");
                if (!string.IsNullOrWhiteSpace(color)) await ExecCommand("foreColor", color);
                break;
            }
        }
    }

    private async Task ExecCommand(string command, string? arg = null)
    {
        try
        {
            // document.execCommand(command, showUI, value?)
            await JS.InvokeVoidAsync("document.execCommand", command, false, arg ?? "");
        }
        catch (JSException)
        {
            // Quietly ignore — execCommand failures are best-effort in MVP.
        }
    }

    private static string GetToolLabel(EditorToolbarItem tool) => tool switch
    {
        EditorToolbarItem.Bold => "Bold",
        EditorToolbarItem.Italic => "Italic",
        EditorToolbarItem.Underline => "Underline",
        EditorToolbarItem.BulletList => "Bulleted list",
        EditorToolbarItem.NumberedList => "Numbered list",
        EditorToolbarItem.Heading => "Heading",
        EditorToolbarItem.Link => "Insert link",
        EditorToolbarItem.Image => "Insert image",
        EditorToolbarItem.Undo => "Undo",
        EditorToolbarItem.Redo => "Redo",
        EditorToolbarItem.FontSize => "Font size",
        EditorToolbarItem.Color => "Text color",
        _ => tool.ToString(),
    };

    private static string GetToolGlyph(EditorToolbarItem tool) => tool switch
    {
        EditorToolbarItem.Bold => "B",
        EditorToolbarItem.Italic => "I",
        EditorToolbarItem.Underline => "U",
        EditorToolbarItem.BulletList => "•",
        EditorToolbarItem.NumberedList => "1.",
        EditorToolbarItem.Heading => "H",
        EditorToolbarItem.Link => "🔗",
        EditorToolbarItem.Image => "🖼",
        EditorToolbarItem.Undo => "↶",
        EditorToolbarItem.Redo => "↷",
        EditorToolbarItem.FontSize => "A↕",
        EditorToolbarItem.Color => "A",
        _ => tool.ToString(),
    };
}
