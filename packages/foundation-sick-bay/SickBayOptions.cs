using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Host-configurable Sick Bay tunables per ADR 0082 §7.
/// </summary>
public sealed class SickBayOptions
{
    /// <summary>
    /// Tenant-registered field-purpose keys + display names. Case-
    /// insensitive lookup; empty by default. Hosts populate via
    /// <see cref="RegisterPurpose"/> at DI configuration time.
    /// </summary>
    public IDictionary<string, string> RegisteredFieldPurposes { get; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Polling cadence used by
    /// <see cref="ISickBayDataProvider.SubscribeSnapshotAsync"/>
    /// implementations falling back to polling when no push transport
    /// is available. Default 60 seconds.
    /// </summary>
    public TimeSpan FallbackPollingInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Register a field-purpose key + display name. Idempotent on
    /// <paramref name="purpose"/>; re-registering overwrites the prior
    /// display name.
    /// </summary>
    public SickBayOptions RegisterPurpose(string purpose, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentNullException.ThrowIfNull(friendlyName);
        RegisteredFieldPurposes[purpose] = friendlyName;
        return this;
    }
}
