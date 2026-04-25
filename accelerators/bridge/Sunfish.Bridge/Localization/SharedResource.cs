namespace Sunfish.Bridge.Localization;

/// <summary>
/// Marker type for <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>
/// against the Sunfish.Bridge shared resource bundle. The type itself carries no
/// members — IStringLocalizer{T} only uses the generic parameter as a correlation
/// handle to find the matching <c>Resources/Localization/SharedResource.resx</c>.
/// </summary>
public class SharedResource
{
}
