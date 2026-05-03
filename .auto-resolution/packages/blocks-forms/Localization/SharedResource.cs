namespace Sunfish.Blocks.Forms.Localization;

/// <summary>
/// Marker type for <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>
/// against the Sunfish.Blocks.Forms shared resource bundle. The type itself carries
/// no members — IStringLocalizer{T} only uses the generic parameter as a correlation
/// handle to find the matching <c>Resources/Localization/SharedResource.resx</c>.
/// </summary>
/// <remarks>
/// Wave 2 Cluster C skeleton (Plan 2 Task 3.5). Pattern B package — Razor SDK with
/// no DI surface; the marker + bundle ship together but the open-generic
/// <c>ISunfishLocalizer&lt;&gt;</c> registration is wired by downstream consumers
/// (apps / accelerators) at composition. Plan 6 replaces the scaffold pilot strings
/// with form-block end-user content (form rendering, validation messages, submission
/// flows).
/// </remarks>
public sealed class SharedResource
{
}
