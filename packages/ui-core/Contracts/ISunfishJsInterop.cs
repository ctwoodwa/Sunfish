namespace Sunfish.UICore.Contracts;

/// <summary>
/// Framework-agnostic contract for Sunfish JavaScript interop operations.
/// </summary>
/// <remarks>
/// The Blazor adapter implementation extends this with Blazor-specific overloads
/// (e.g., <c>GetElementBoundsAsync(ElementReference)</c> and
/// <c>ObserveScrollAsync(ElementReference, DotNetObjectReference)</c>)
/// that are not part of this contract because they reference Blazor types.
/// </remarks>
public interface ISunfishJsInterop : IAsyncDisposable
{
    /// <summary>Initializes the JS module. Must be called before other methods.</summary>
    ValueTask InitializeAsync();

    /// <summary>Shows the modal with the given HTML element ID.</summary>
    /// <returns><c>true</c> if the modal was shown; <c>false</c> if not found.</returns>
    ValueTask<bool> ShowModalAsync(string modalId);

    /// <summary>Hides the modal with the given HTML element ID.</summary>
    ValueTask HideModalAsync(string modalId);

    /// <summary>
    /// Returns the bounding box of the element with the given HTML element ID.
    /// </summary>
    /// <param name="elementId">The HTML id attribute value of the target element.</param>
    ValueTask<BoundingBox> GetElementBoundsAsync(string elementId);
}

/// <summary>
/// The bounding box of a DOM element as reported by <c>getBoundingClientRect()</c>.
/// </summary>
public record BoundingBox(double X, double Y, double Width, double Height);
