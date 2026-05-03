using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Sunfish.Analyzers.CompatVendorUsings;

/// <summary>
/// Data-only mapping table: vendor namespace → Sunfish.Compat.* replacement
/// (or a "no shim available" sentinel for vendors Sunfish intentionally does
/// not support, e.g. DevExpress).
///
/// Matching strategy (see <see cref="TryResolve"/>):
///   1. Exact match on the full namespace (e.g. "Telerik.Blazor.Components").
///   2. Prefix match if the registry entry is marked <see cref="VendorEntry.PrefixMatch"/>
///      — used for vendor roots whose component namespaces live in arbitrarily nested
///      child namespaces (e.g. "Syncfusion.Blazor", "Syncfusion.Blazor.Grids",
///      "Syncfusion.Blazor.Buttons" all collapse to "Sunfish.Compat.Syncfusion").
///
/// Prefix matching intentionally requires the match to be followed by either
/// end-of-string or a '.' separator so that "SyncfusionFake.Blazor" does not
/// accidentally match the "Syncfusion.Blazor" prefix.
/// </summary>
internal static class VendorNamespaceRegistry
{
    /// <summary>
    /// One row in the registry. <see cref="Namespace"/> is compared against the
    /// <c>using</c> directive's qualified name; <see cref="Replacement"/> is the
    /// Sunfish.Compat.* namespace we suggest migrating to (or <c>null</c> when
    /// <see cref="Kind"/> is <see cref="VendorEntryKind.NoShimAvailable"/>).
    /// </summary>
    public readonly struct VendorEntry
    {
        public VendorEntry(
            string ns,
            string? replacement,
            bool prefixMatch,
            VendorEntryKind kind,
            string? migrationGuide = null)
        {
            Namespace = ns;
            Replacement = replacement;
            PrefixMatch = prefixMatch;
            Kind = kind;
            MigrationGuide = migrationGuide;
        }

        public string Namespace { get; }
        public string? Replacement { get; }
        public bool PrefixMatch { get; }
        public VendorEntryKind Kind { get; }
        public string? MigrationGuide { get; }
    }

    public enum VendorEntryKind
    {
        /// <summary>SF0001 — compat shim exists, code fix available.</summary>
        CompatShimAvailable,

        /// <summary>SF0002 — vendor recognized, but no compat shim (manual migration).</summary>
        NoShimAvailable,
    }

    /// <summary>
    /// The full registry. Order does not matter semantically, but we keep exact
    /// matches before prefix matches because <see cref="TryResolve"/> short-circuits
    /// on the first match.
    ///
    /// Sources:
    ///   - Commercial-vendor compat packages: packages/compat-telerik, compat-syncfusion,
    ///     compat-infragistics.
    ///   - Icon-compat packages: packages/compat-font-awesome, compat-material-icons,
    ///     compat-fluent-icons, compat-bootstrap-icons, compat-lucide, compat-heroicons,
    ///     compat-octicons.
    ///   - Dropped: DevExpress per icm/00_intake/output/compat-expansion-intake.md §9
    ///     Scope reduction — 2026-04-22.
    /// </summary>
    public static readonly ImmutableArray<VendorEntry> Entries = ImmutableArray.Create(
        // === Commercial Blazor component vendors (SF0001, has code fix) ===

        // Telerik UI for Blazor — native Blazor, single canonical namespace.
        new VendorEntry(
            ns: "Telerik.Blazor.Components",
            replacement: "Sunfish.Compat.Telerik",
            prefixMatch: false,
            kind: VendorEntryKind.CompatShimAvailable),

        // Syncfusion Blazor — surface spans many child namespaces:
        // Syncfusion.Blazor.Buttons, .Grids, .Inputs, .Calendars, .DropDowns, etc.
        // We collapse all "Syncfusion.Blazor" and "Syncfusion.Blazor.*" onto a
        // single replacement.
        new VendorEntry(
            ns: "Syncfusion.Blazor",
            replacement: "Sunfish.Compat.Syncfusion",
            prefixMatch: true,
            kind: VendorEntryKind.CompatShimAvailable),

        // Infragistics Ignite UI for Blazor — surface spans many child namespaces
        // (IgniteUI.Blazor.Controls, IgniteUI.Blazor.*); same collapse pattern.
        new VendorEntry(
            ns: "IgniteUI.Blazor",
            replacement: "Sunfish.Compat.Infragistics",
            prefixMatch: true,
            kind: VendorEntryKind.CompatShimAvailable),

        // === Icon-library compats (SF0001, has code fix) ===

        new VendorEntry(
            ns: "Blazored.FontAwesome",
            replacement: "Sunfish.Compat.FontAwesome",
            prefixMatch: false,
            kind: VendorEntryKind.CompatShimAvailable),

        new VendorEntry(
            ns: "FontAwesome.Sharp",
            replacement: "Sunfish.Compat.FontAwesome",
            prefixMatch: false,
            kind: VendorEntryKind.CompatShimAvailable),

        new VendorEntry(
            ns: "Microsoft.FluentUI.AspNetCore.Components.Icons",
            replacement: "Sunfish.Compat.FluentIcons",
            prefixMatch: false,
            kind: VendorEntryKind.CompatShimAvailable),

        // Google Material Icons / Material Symbols wrapper (various community packages
        // cluster under the MaterialDesign / MatIcon namespaces). We target the MatIcon
        // wrapper's canonical namespace; users of other wrappers can be added on request.
        new VendorEntry(
            ns: "MatBlazor",
            replacement: "Sunfish.Compat.MaterialIcons",
            prefixMatch: true,
            kind: VendorEntryKind.CompatShimAvailable),

        // Blazicons family: Blazicons.Lucide → Sunfish.Compat.Lucide.
        new VendorEntry(
            ns: "Blazicons.Lucide",
            replacement: "Sunfish.Compat.Lucide",
            prefixMatch: false,
            kind: VendorEntryKind.CompatShimAvailable),

        // Heroicons Blazor wrapper. Community packages cluster under Heroicons.*
        new VendorEntry(
            ns: "Heroicons.Blazor",
            replacement: "Sunfish.Compat.Heroicons",
            prefixMatch: true,
            kind: VendorEntryKind.CompatShimAvailable),

        // Octicons Blazor wrapper.
        new VendorEntry(
            ns: "Octicons.Blazor",
            replacement: "Sunfish.Compat.Octicons",
            prefixMatch: true,
            kind: VendorEntryKind.CompatShimAvailable),

        // BlazorBootstrap — intentionally scoped NARROWLY to icon-bearing sub-namespaces
        // only. We do NOT want to flag the whole BlazorBootstrap surface, which
        // covers non-icon components that Sunfish does not have a BootstrapIcons
        // replacement for. Flagging only BlazorBootstrap.Icons keeps the rule honest.
        new VendorEntry(
            ns: "BlazorBootstrap.Icons",
            replacement: "Sunfish.Compat.BootstrapIcons",
            prefixMatch: true,
            kind: VendorEntryKind.CompatShimAvailable),

        // === No-compat-available (SF0002, no code fix) ===

        // DevExpress Blazor — dropped from compat scope 2026-04-22; the analyzer still
        // flags usings so DX migrators get a nudge toward the manual-migration doc.
        new VendorEntry(
            ns: "DevExpress.Blazor",
            replacement: null,
            prefixMatch: true,
            kind: VendorEntryKind.NoShimAvailable,
            migrationGuide: "docs/devexpress-migration.md")
    );

    /// <summary>
    /// Attempts to resolve a using-directive qualified name to a registry entry.
    /// Returns true if a match is found (exact or prefix).
    /// </summary>
    public static bool TryResolve(string usingNamespace, out VendorEntry entry)
    {
        if (string.IsNullOrEmpty(usingNamespace))
        {
            entry = default;
            return false;
        }

        // Exact matches win over prefix matches. Two passes so registry ordering
        // within an entry kind doesn't matter.
        foreach (var candidate in Entries)
        {
            if (!candidate.PrefixMatch &&
                string.Equals(candidate.Namespace, usingNamespace, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        foreach (var candidate in Entries)
        {
            if (candidate.PrefixMatch && IsPrefixMatch(usingNamespace, candidate.Namespace))
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="candidate"/> equals <paramref name="prefix"/>
    /// OR starts with "<paramref name="prefix"/>." — i.e. the match terminates at a
    /// namespace boundary, never mid-identifier.
    /// </summary>
    private static bool IsPrefixMatch(string candidate, string prefix)
    {
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (candidate.Length == prefix.Length)
        {
            return true;
        }

        return candidate[prefix.Length] == '.';
    }

    /// <summary>
    /// Returns a read-only view of all registered vendor namespaces (for logging / docs).
    /// </summary>
    public static IEnumerable<string> AllVendorNamespaces()
    {
        foreach (var entry in Entries)
        {
            yield return entry.Namespace;
        }
    }
}
