using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Evaluates first-party caveat predicates against a <see cref="MacaroonContext"/>.
/// </summary>
/// <remarks>
/// <para>Supported predicate grammar (exact regex forms below):</para>
/// <list type="bullet">
///   <item><c>time &lt; "ISO8601"</c> or <c>time &lt;= "ISO8601"</c> — deadline comparison against
///   <see cref="MacaroonContext.Now"/>.</item>
///   <item><c>subject == "uri"</c> — ordinal equality against
///   <see cref="MacaroonContext.SubjectUri"/>.</item>
///   <item><c>resource.schema matches "glob"</c> — glob (<c>*</c> wildcard) against
///   <see cref="MacaroonContext.ResourceSchema"/>.</item>
///   <item><c>action in ["a", "b", ...]</c> — membership check against
///   <see cref="MacaroonContext.RequestedAction"/>.</item>
///   <item><c>device_ip in "cidr"</c> — IPv4 CIDR containment against
///   <see cref="MacaroonContext.DeviceIp"/>.</item>
/// </list>
/// <para>Unknown predicates, missing context fields, and malformed values all fail closed
/// (return <c>false</c>).</para>
/// </remarks>
internal static partial class FirstPartyCaveatParser
{
    // time <= "2026-12-31T23:59:59Z"   (also accepts bare < )
    [GeneratedRegex("""^\s*time\s*<=?\s*"([^"]+)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex TimeRegex();

    // subject == "urn:sunfish:subject:alice"
    [GeneratedRegex("""^\s*subject\s*==\s*"([^"]+)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex SubjectRegex();

    // resource.schema matches "sunfish.pm.*"
    [GeneratedRegex("""^\s*resource\.schema\s+matches\s+"([^"]+)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex SchemaMatchesRegex();

    // action in ["read", "write"]
    [GeneratedRegex("""^\s*action\s+in\s+\[\s*(.*?)\s*\]\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex ActionInRegex();

    // device_ip in "10.42.0.0/16"
    [GeneratedRegex("""^\s*device_ip\s+in\s+"([^"]+)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceIpInRegex();

    /// <summary>
    /// Evaluates <paramref name="caveat"/> against <paramref name="ctx"/>. Returns <c>true</c>
    /// only when the predicate is recognised AND its required context field is populated AND
    /// the condition holds. Every other outcome returns <c>false</c> (fail closed).
    /// </summary>
    public static bool Evaluate(Caveat caveat, MacaroonContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var predicate = caveat.Predicate ?? string.Empty;

        var m = TimeRegex().Match(predicate);
        if (m.Success)
        {
            if (!DateTimeOffset.TryParse(
                    m.Groups[1].Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var limit))
                return false;
            return ctx.Now <= limit;
        }

        m = SubjectRegex().Match(predicate);
        if (m.Success)
        {
            if (ctx.SubjectUri is null) return false;
            return string.Equals(ctx.SubjectUri, m.Groups[1].Value, StringComparison.Ordinal);
        }

        m = SchemaMatchesRegex().Match(predicate);
        if (m.Success)
        {
            if (ctx.ResourceSchema is null) return false;
            var glob = m.Groups[1].Value;
            var pattern = "^" + Regex.Escape(glob).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(ctx.ResourceSchema, pattern, RegexOptions.CultureInvariant);
        }

        m = ActionInRegex().Match(predicate);
        if (m.Success)
        {
            if (ctx.RequestedAction is null) return false;
            var body = m.Groups[1].Value;
            var items = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item.Length >= 2 && item[0] == '"' && item[^1] == '"')
                    item = item[1..^1];
                if (string.Equals(item, ctx.RequestedAction, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        m = DeviceIpInRegex().Match(predicate);
        if (m.Success)
        {
            if (ctx.DeviceIp is null) return false;
            return IsInCidr(ctx.DeviceIp, m.Groups[1].Value);
        }

        // Unknown caveat → fail closed.
        return false;
    }

    private static bool IsInCidr(string ip, string cidr)
    {
        var slash = cidr.IndexOf('/');
        if (slash < 0) return false;
        if (!IPAddress.TryParse(cidr[..slash], out var net)) return false;
        if (!int.TryParse(cidr[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
            return false;
        if (!IPAddress.TryParse(ip, out var ipAddr)) return false;

        var netBytes = net.GetAddressBytes();
        var ipBytes = ipAddr.GetAddressBytes();
        if (ipBytes.Length != netBytes.Length) return false;
        if (prefix < 0 || prefix > netBytes.Length * 8) return false;

        for (var i = 0; i < netBytes.Length; i++)
        {
            var bitsInThisByte = Math.Min(8, prefix - i * 8);
            if (bitsInThisByte <= 0) break;
            var mask = bitsInThisByte == 8 ? 0xFF : (0xFF << (8 - bitsInThisByte)) & 0xFF;
            if ((netBytes[i] & mask) != (ipBytes[i] & mask)) return false;
        }
        return true;
    }
}
