namespace Sunfish.Blocks.Tasks.Localization;

/// <summary>
/// Marker type for <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>
/// against the Sunfish.Blocks.Tasks shared resource bundle. The type itself carries
/// no members — IStringLocalizer{T} only uses the generic parameter as a correlation
/// handle to find the matching <c>Resources/Localization/SharedResource.resx</c>.
/// </summary>
/// <remarks>
/// Wave 2 Cluster C skeleton (Plan 2 Task 3.5). Pattern B package — Razor SDK with
/// no DI surface; the marker + bundle ship together but the open-generic
/// <c>ISunfishLocalizer&lt;&gt;</c> registration is wired by downstream consumers
/// (apps / accelerators) at composition. Plan 6 replaces the scaffold pilot strings
/// with task-block end-user content (task board, kanban transitions, due-date /
/// assignee labels).
/// </remarks>
public sealed class SharedResource
{
}
