namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Layout direction exposed by <see cref="Components.Utility.SunfishThemeProvider"/> as a
/// cascading value to descendant Sunfish components. Maps 1:1 to the HTML <c>dir</c>
/// attribute, with <see cref="Auto"/> letting the browser choose per the document
/// and the bidi algorithm.
/// </summary>
public enum TextDirection
{
    /// <summary>Left-to-right layout. Emits <c>dir="ltr"</c> and a cascaded <c>Ltr</c> value.</summary>
    Ltr,

    /// <summary>Right-to-left layout. Emits <c>dir="rtl"</c> and a cascaded <c>Rtl</c> value.</summary>
    Rtl,

    /// <summary>Let the user agent pick the direction (<c>dir="auto"</c>).</summary>
    Auto
}
