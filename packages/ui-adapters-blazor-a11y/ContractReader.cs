using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Reads <c>a11y-contracts.json</c> emitted by the ui-core build's
/// <c>export-a11y-contracts.mts</c> script (Plan 4 Task 1.6) and returns strongly-typed
/// <see cref="SunfishA11yContract"/> records for the Blazor a11y bridge to enforce.
/// </summary>
/// <remarks>
/// File location is auto-discovered (walk up from cwd looking for
/// <c>packages/ui-core/dist/a11y-contracts.json</c>) or set via the
/// <c>SUNFISH_A11Y_CONTRACTS_PATH</c> environment variable in CI.
/// Reader caches the parsed file on first access; call <see cref="Reload"/> if the
/// underlying JSON changes during a long-running test session.
/// </remarks>
public sealed class ContractReader
{
    /// <summary>JSON file path. Override before first use to point at a custom location.</summary>
    public string ContractsPath { get; set; } = ResolveContractsPath();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private Dictionary<string, SunfishA11yContract>? _cache;
    private static readonly Lazy<ContractReader> _default = new(() => new ContractReader());

    /// <summary>Singleton accessor for the default-located contracts file.</summary>
    public static ContractReader Default => _default.Value;

    public ContractReader() { }

    public ContractReader(string contractsPath)
    {
        if (string.IsNullOrEmpty(contractsPath)) throw new ArgumentException("contractsPath required", nameof(contractsPath));
        ContractsPath = contractsPath;
    }

    /// <summary>
    /// Load the contract for the given component tag name (e.g. <c>"sunfish-button"</c>).
    /// </summary>
    /// <exception cref="FileNotFoundException">If the contracts JSON file is missing — typically means
    /// the ui-core build hasn't run; advise <c>pnpm --filter @sunfish/ui-core build:contracts</c>.</exception>
    /// <exception cref="KeyNotFoundException">If the tag has no corresponding entry in the contracts file.</exception>
    public SunfishA11yContract Load(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) throw new ArgumentException("tagName required", nameof(tagName));
        var cache = LoadCache();
        if (!cache.TryGetValue(tagName, out var contract))
        {
            throw new KeyNotFoundException(
                $"No a11y contract for component tag '{tagName}' in {ContractsPath}. " +
                $"Available tags: {string.Join(", ", cache.Keys)}.");
        }
        return contract;
    }

    /// <summary>Returns true if a contract exists for the tag, without throwing.</summary>
    public bool TryLoad(string tagName, out SunfishA11yContract? contract)
    {
        contract = null;
        if (string.IsNullOrEmpty(tagName)) return false;
        var cache = LoadCache();
        return cache.TryGetValue(tagName, out contract);
    }

    /// <summary>Returns every component tag the contracts file knows about.</summary>
    public IReadOnlyCollection<string> AllTags() => LoadCache().Keys;

    /// <summary>Drop the cache so the next <see cref="Load"/> re-reads the JSON.</summary>
    public void Reload() => _cache = null;

    private Dictionary<string, SunfishA11yContract> LoadCache()
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(ContractsPath))
        {
            throw new FileNotFoundException(
                $"Sunfish a11y contracts JSON not found at '{ContractsPath}'. " +
                "Did the ui-core build run? Try: pnpm --filter @sunfish/ui-core build:contracts",
                ContractsPath);
        }

        var json = File.ReadAllText(ContractsPath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, SunfishA11yContract>>(json, JsonOptions)
            ?? new Dictionary<string, SunfishA11yContract>();
        _cache = dict;
        return _cache;
    }

    private static string ResolveContractsPath()
    {
        var envPath = Environment.GetEnvironmentVariable("SUNFISH_A11Y_CONTRACTS_PATH");
        if (!string.IsNullOrEmpty(envPath)) return envPath;

        // Walk up from cwd looking for packages/ui-core/dist/a11y-contracts.json.
        var current = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(current); i++)
        {
            var candidate = Path.Combine(current, "packages", "ui-core", "dist", "a11y-contracts.json");
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current);
        }

        // Sentinel — caller will see a clear error if contracts are missing.
        return Path.Combine("packages", "ui-core", "dist", "a11y-contracts.json");
    }
}
