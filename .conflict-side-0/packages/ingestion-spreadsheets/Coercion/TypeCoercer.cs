using System.Globalization;
using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Spreadsheets.Coercion;

/// <summary>
/// Stateless coercion helpers that turn a raw string cell value into a strongly typed
/// <see cref="object"/> box according to the requested <see cref="CoercionKind"/>.
/// </summary>
public static class TypeCoercer
{
    /// <summary>
    /// Attempts to coerce <paramref name="raw"/> to the given <paramref name="kind"/>. Returns a
    /// successful <see cref="IngestionResult{T}"/> carrying the boxed value, or a
    /// <see cref="IngestOutcome.ValidationFailed"/> failure with a descriptive message.
    /// </summary>
    public static IngestionResult<object?> TryCoerce(string raw, CoercionKind kind)
    {
        switch (kind)
        {
            case CoercionKind.String:
                return IngestionResult<object?>.Success(raw);

            case CoercionKind.Integer:
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return IngestionResult<object?>.Success(i);
                return IngestionResult<object?>.Fail(IngestOutcome.ValidationFailed, $"Could not coerce '{raw}' to Integer.");

            case CoercionKind.Decimal:
                if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    return IngestionResult<object?>.Success(d);
                return IngestionResult<object?>.Fail(IngestOutcome.ValidationFailed, $"Could not coerce '{raw}' to Decimal.");

            case CoercionKind.DateTimeUtc:
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                    return IngestionResult<object?>.Success(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
                return IngestionResult<object?>.Fail(IngestOutcome.ValidationFailed, $"Could not coerce '{raw}' to DateTimeUtc.");

            case CoercionKind.Boolean:
                var b = raw.Trim().ToLowerInvariant();
                return b switch
                {
                    "true" or "yes" or "1" => IngestionResult<object?>.Success(true),
                    "false" or "no" or "0" => IngestionResult<object?>.Success(false),
                    _ => IngestionResult<object?>.Fail(IngestOutcome.ValidationFailed, $"Could not coerce '{raw}' to Boolean."),
                };

            case CoercionKind.EnumIgnoreCase:
                // Produces a normalized string; actual enum resolution is the consumer's job.
                return IngestionResult<object?>.Success(raw.Trim());

            default:
                return IngestionResult<object?>.Fail(IngestOutcome.InternalError, $"Unknown coercion kind '{kind}'.");
        }
    }
}
