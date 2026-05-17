using System.Collections.Generic;
using System.Linq;
using Sunfish.Blocks.Reports.Exceptions;

namespace Sunfish.Blocks.Reports;

/// <summary>
/// In-memory registry of <see cref="IReportCartridge{TParams,TResult}"/>
/// implementations keyed by
/// <c>(ReportKind, paramsType, resultType)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Keying by all three defends against accidental param/result-type
/// mismatch at registration time — a common bug source in
/// generic-dispatch registries.
/// </para>
/// <para>
/// <b>Thread-safety contract.</b> <see cref="Register{TParams,TResult}"/>
/// is NOT thread-safe; all <see cref="Register{TParams,TResult}"/>
/// calls MUST complete at DI startup before any
/// <see cref="Resolve{TParams,TResult}"/> /
/// <see cref="TryResolve{TParams,TResult}"/> /
/// <see cref="RegisteredKinds"/> read. Concurrent reads after
/// startup are safe (lock-free dictionary lookups). The DI extension
/// <see cref="DependencyInjection.ReportSubstrateServiceCollectionExtensions.AddBlocksReportsSubstrate"/>
/// registers the type as <c>Singleton</c> and cartridge registrations
/// happen at host bootstrap — the convention is observed at the
/// cluster level. Per council SE-2 + A.2 (PR #980 review).
/// </para>
/// </remarks>
public sealed class ReportCartridgeRegistry
{
    private readonly Dictionary<(ReportKind kind, System.Type paramsType, System.Type resultType), object> _cartridges = new();

    /// <summary>Register a cartridge implementation. Duplicates throw.</summary>
    public void Register<TParams, TResult>(IReportCartridge<TParams, TResult> cartridge)
        where TParams : class
        where TResult : class
    {
        if (cartridge is null) throw new System.ArgumentNullException(nameof(cartridge));
        var key = (cartridge.Kind, typeof(TParams), typeof(TResult));
        if (_cartridges.ContainsKey(key))
            throw new System.InvalidOperationException(
                $"Cartridge already registered for ReportKind={key.Item1} (TParams={key.Item2.Name}, TResult={key.Item3.Name}).");
        _cartridges[key] = cartridge;
    }

    /// <summary>Resolve a cartridge by kind + TParams + TResult.</summary>
    /// <exception cref="UnknownReportKindException">No matching cartridge registered.</exception>
    public IReportCartridge<TParams, TResult> Resolve<TParams, TResult>(ReportKind kind)
        where TParams : class
        where TResult : class
    {
        if (_cartridges.TryGetValue((kind, typeof(TParams), typeof(TResult)), out var cartridge))
            return (IReportCartridge<TParams, TResult>)cartridge;
        throw new UnknownReportKindException(kind, typeof(TParams), typeof(TResult));
    }

    /// <summary>Try-resolve a cartridge by kind + TParams + TResult.</summary>
    public bool TryResolve<TParams, TResult>(
        ReportKind kind,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out IReportCartridge<TParams, TResult> cartridge)
        where TParams : class
        where TResult : class
    {
        if (_cartridges.TryGetValue((kind, typeof(TParams), typeof(TResult)), out var raw))
        {
            cartridge = (IReportCartridge<TParams, TResult>)raw;
            return true;
        }
        cartridge = null;
        return false;
    }

    /// <summary>Snapshot of all distinct <see cref="ReportKind"/>s that have at least one registered cartridge.</summary>
    public IReadOnlyList<ReportKind> RegisteredKinds =>
        _cartridges.Keys.Select(k => k.kind).Distinct().ToArray();
}
