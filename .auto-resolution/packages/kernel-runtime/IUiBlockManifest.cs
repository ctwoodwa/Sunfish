namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Paper §5.3 extension-point contract. Registers a UI block with the UI
/// kernel and declares the streams + attestations it requires.
/// </summary>
public interface IUiBlockManifest
{
    /// <summary>Stable identifier for this block.</summary>
    string BlockId { get; }

    /// <summary>Human-readable display name shown in the block picker.</summary>
    string DisplayName { get; }

    /// <summary>Category label used for grouping in the UI (e.g., "Accounting", "Scheduling").</summary>
    string Category { get; }

    /// <summary>Stream IDs this block reads from. Used for subscription filtering.</summary>
    IReadOnlyCollection<string> RequiredStreamIds { get; }

    /// <summary>Role attestations the current user must hold to render this block.</summary>
    IReadOnlyCollection<string> RequiredAttestations { get; }
}
