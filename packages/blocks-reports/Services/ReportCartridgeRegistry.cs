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
/// Keying by all three defends against accidental param/result-type
/// mismatch at registration time — a common bug source in
/// generic-dispatch registries.
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
    public bool TryResolve<TParams, TResult>(ReportKind kind, out IReportCartridge<TParams, TResult>? cartridge)
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
