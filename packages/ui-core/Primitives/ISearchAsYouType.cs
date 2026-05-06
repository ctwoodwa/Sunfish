using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.UICore.Primitives;

/// <summary>
/// Search-as-you-type primitive per ADR 0077 §4 + W#55 P2/P3 search
/// bar. Adapter implementations debounce + cancel in-flight queries
/// when the user types ahead.
/// </summary>
/// <typeparam name="T">Result item type.</typeparam>
public interface ISearchAsYouType<T>
{
    /// <summary>
    /// Returns matches for <paramref name="query"/>. Implementations
    /// SHOULD debounce (typical 150-250ms) + honour
    /// <paramref name="ct"/> to cancel stale queries.
    /// </summary>
    ValueTask<IReadOnlyList<T>> SearchAsync(string query, CancellationToken ct = default);
}
