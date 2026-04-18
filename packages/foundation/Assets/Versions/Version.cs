using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Versions;

/// <summary>
/// A single version of an entity.
/// </summary>
/// <remarks>
/// Spec §3.2. Phase A notes:
/// <list type="bullet">
///   <item><description>
///     <paramref name="Signature"/> is nullable (plan D-NULLABLE-SIGNATURES — Ed25519 signing is Phase B).
///   </description></item>
///   <item><description>
///     <paramref name="Diff"/> is nullable and unpopulated — the full <paramref name="Body"/>
///     is authoritative. JSON Patch diffs are a Phase B compact-storage optimisation.
///   </description></item>
///   <item><description>
///     <paramref name="ValidTo"/> is <c>null</c> for the tip of the version chain. When a newer
///     version is appended the previous row's <paramref name="ValidTo"/> is set to the new version's
///     <paramref name="ValidFrom"/> so validity ranges are contiguous.
///   </description></item>
/// </list>
/// </remarks>
public sealed record Version(
    VersionId Id,
    VersionId? ParentId,
    JsonDocument Body,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    ActorId Author,
    byte[]? Signature,
    JsonDocument? Diff);
