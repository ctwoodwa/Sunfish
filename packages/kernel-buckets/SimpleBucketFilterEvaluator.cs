namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Minimal filter evaluator covering the paper §10.2 examples. Grammar:
/// <code>
/// expr     := term ( 'AND' term )*
/// term     := field op literal
/// field    := ident ( '.' ident )?       // e.g. record.team_id, project.archived
/// op       := '=' | '!='
/// literal  := ident | number | 'true' | 'false' | quoted-string
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Limitations (explicit; intended to evolve in a later wave):
/// </para>
/// <list type="bullet">
///   <item><description>No OR, NOT, parentheses, or operator precedence.</description></item>
///   <item><description>No comparison operators beyond <c>=</c> / <c>!=</c> — no <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>IN</c>, <c>LIKE</c>.</description></item>
///   <item><description>No function calls, no arithmetic, no date arithmetic.</description></item>
///   <item><description>Right-hand side may be a literal or a dotted field reference. A dotted identifier (e.g. <c>peer.team_id</c>) resolves from the context bag; a bare identifier is a literal (<c>true</c> / <c>false</c> / <c>null</c> or an unquoted string). This covers the paper's one cross-field example <c>record.team_id = peer.team_id</c>.</description></item>
///   <item><description>Identifiers are ASCII letters, digits, and underscore only.</description></item>
///   <item><description>Missing fields resolve to <c>null</c>; an equality test against any non-null literal returns <c>false</c>.</description></item>
///   <item><description>String comparisons are ordinal and case-sensitive.</description></item>
/// </list>
/// <para>
/// No arbitrary user-input code execution — the grammar is a fixed tokenizer + recursive-descent
/// parser. The evaluator never calls <c>Eval</c> or a scripting engine.
/// </para>
/// </remarks>
public sealed class SimpleBucketFilterEvaluator : IBucketFilterEvaluator
{
    /// <inheritdoc />
    public bool Evaluate(string? filter, IReadOnlyDictionary<string, object?> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var tokens = Tokenize(filter);
        var pos = 0;
        var result = ParseAndExpression(tokens, ref pos, context);

        if (pos < tokens.Count)
        {
            throw new BucketFilterSyntaxException(
                $"Unexpected trailing token '{tokens[pos].Text}' at position {tokens[pos].Position} in filter '{filter}'.");
        }

        return result;
    }

    // --- Parser -----------------------------------------------------------

    private static bool ParseAndExpression(List<Token> tokens, ref int pos, IReadOnlyDictionary<string, object?> context)
    {
        var left = ParseComparison(tokens, ref pos, context);
        while (pos < tokens.Count && tokens[pos].Kind == TokenKind.And)
        {
            pos++;
            var right = ParseComparison(tokens, ref pos, context);
            left = left && right;
        }
        return left;
    }

    private static bool ParseComparison(List<Token> tokens, ref int pos, IReadOnlyDictionary<string, object?> context)
    {
        if (pos >= tokens.Count || tokens[pos].Kind != TokenKind.Identifier)
        {
            throw new BucketFilterSyntaxException(
                pos >= tokens.Count
                    ? "Unexpected end of filter expression; expected a field identifier."
                    : $"Expected field identifier, got '{tokens[pos].Text}' at position {tokens[pos].Position}.");
        }

        var fieldToken = tokens[pos++];
        var fieldPath = fieldToken.Text;

        if (pos >= tokens.Count)
        {
            throw new BucketFilterSyntaxException(
                $"Unexpected end of filter expression after field '{fieldPath}'; expected '=' or '!='.");
        }

        var opToken = tokens[pos];
        bool invert;
        if (opToken.Kind == TokenKind.Equal) { invert = false; }
        else if (opToken.Kind == TokenKind.NotEqual) { invert = true; }
        else
        {
            throw new BucketFilterSyntaxException(
                $"Unknown operator '{opToken.Text}' at position {opToken.Position}. Only '=' and '!=' are supported.");
        }
        pos++;

        if (pos >= tokens.Count)
        {
            throw new BucketFilterSyntaxException(
                $"Unexpected end of filter expression after operator '{opToken.Text}'; expected a literal value.");
        }

        var rhsToken = tokens[pos++];
        if (rhsToken.Kind != TokenKind.Identifier &&
            rhsToken.Kind != TokenKind.Number &&
            rhsToken.Kind != TokenKind.QuotedString)
        {
            throw new BucketFilterSyntaxException(
                $"Expected literal value at position {rhsToken.Position}, got '{rhsToken.Text}'.");
        }

        var fieldValue = context.TryGetValue(fieldPath, out var v) ? v : null;

        // A dotted identifier on the RHS (e.g. `peer.team_id`) is a field reference,
        // resolved from the context bag. Bare identifiers remain literal (true/false/null
        // or an unquoted string). The paper's §10.2 example `record.team_id = peer.team_id`
        // relies on this cross-field resolution.
        object? rhsValue;
        if (rhsToken.Kind == TokenKind.Identifier && rhsToken.Text.Contains('.'))
        {
            rhsValue = context.TryGetValue(rhsToken.Text, out var rv) ? rv : null;
        }
        else
        {
            rhsValue = CoerceLiteral(rhsToken);
        }

        var equal = ValuesEqual(fieldValue, rhsValue);
        return invert ? !equal : equal;
    }

    private static object? CoerceLiteral(Token t)
    {
        switch (t.Kind)
        {
            case TokenKind.QuotedString:
                return t.Text;
            case TokenKind.Number:
                return long.TryParse(t.Text, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var l)
                    ? l
                    : (object)double.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture);
            case TokenKind.Identifier:
                // Bare identifiers act as keyword literals: true/false, otherwise treat as string.
                if (string.Equals(t.Text, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(t.Text, "false", StringComparison.OrdinalIgnoreCase)) return false;
                if (string.Equals(t.Text, "null", StringComparison.OrdinalIgnoreCase)) return null;
                return t.Text;
            default:
                throw new BucketFilterSyntaxException(
                    $"Cannot coerce token '{t.Text}' at position {t.Position} to a literal.");
        }
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        // Numeric coercion so `age = 90` matches int/long/double contexts.
        if (a is IConvertible && b is IConvertible && (IsNumeric(a) || IsNumeric(b)))
        {
            try
            {
                var da = Convert.ToDouble(a, System.Globalization.CultureInfo.InvariantCulture);
                var db = Convert.ToDouble(b, System.Globalization.CultureInfo.InvariantCulture);
                return da == db;
            }
            catch
            {
                // fall through to string compare
            }
        }

        // Boolean literals compared against boolean context values.
        if (a is bool ab && b is bool bb) return ab == bb;

        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool IsNumeric(object o) =>
        o is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    // --- Tokenizer --------------------------------------------------------

    private enum TokenKind { Identifier, Number, QuotedString, Equal, NotEqual, And }

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    private static List<Token> Tokenize(string filter)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < filter.Length)
        {
            var c = filter[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '=')
            {
                tokens.Add(new Token(TokenKind.Equal, "=", i));
                i++;
            }
            else if (c == '!' && i + 1 < filter.Length && filter[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.NotEqual, "!=", i));
                i += 2;
            }
            else if (c == '\'' || c == '"')
            {
                var quote = c;
                var start = i + 1;
                i++;
                while (i < filter.Length && filter[i] != quote) i++;
                if (i >= filter.Length)
                {
                    throw new BucketFilterSyntaxException(
                        $"Unterminated string literal starting at position {start - 1} in filter '{filter}'.");
                }
                tokens.Add(new Token(TokenKind.QuotedString, filter.Substring(start, i - start), start - 1));
                i++; // consume closing quote
            }
            else if (char.IsDigit(c) || (c == '-' && i + 1 < filter.Length && char.IsDigit(filter[i + 1])))
            {
                var start = i;
                if (c == '-') i++;
                while (i < filter.Length && (char.IsDigit(filter[i]) || filter[i] == '.')) i++;
                tokens.Add(new Token(TokenKind.Number, filter.Substring(start, i - start), start));
            }
            else if (IsIdentStart(c))
            {
                var start = i;
                while (i < filter.Length && (IsIdentPart(filter[i]) || filter[i] == '.')) i++;
                var text = filter.Substring(start, i - start);
                if (string.Equals(text, "AND", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new Token(TokenKind.And, text, start));
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Identifier, text, start));
                }
            }
            else
            {
                throw new BucketFilterSyntaxException(
                    $"Unexpected character '{c}' at position {i} in filter '{filter}'.");
            }
        }
        return tokens;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
