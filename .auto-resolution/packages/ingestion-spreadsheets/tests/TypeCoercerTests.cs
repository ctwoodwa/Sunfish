using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Spreadsheets;
using Sunfish.Ingestion.Spreadsheets.Coercion;
using Xunit;

namespace Sunfish.Ingestion.Spreadsheets.Tests;

public class TypeCoercerTests
{
    [Fact]
    public void Coerce_String_PassesThrough()
    {
        var r = TypeCoercer.TryCoerce("hello world", CoercionKind.String);
        Assert.True(r.IsSuccess);
        Assert.Equal("hello world", r.Value);
    }

    [Fact]
    public void Coerce_Integer_ParsesValid()
    {
        var r = TypeCoercer.TryCoerce("42", CoercionKind.Integer);
        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Coerce_Integer_FailsOnAlpha()
    {
        var r = TypeCoercer.TryCoerce("studio", CoercionKind.Integer);
        Assert.False(r.IsSuccess);
        Assert.Equal(IngestOutcome.ValidationFailed, r.Outcome);
        Assert.Contains("studio", r.Failure!.Message);
    }

    [Fact]
    public void Coerce_Decimal_ParsesValid()
    {
        var r = TypeCoercer.TryCoerce("123.45", CoercionKind.Decimal);
        Assert.True(r.IsSuccess);
        Assert.Equal(123.45m, r.Value);
    }

    [Fact]
    public void Coerce_DateTimeUtc_ParsesIso8601()
    {
        var r = TypeCoercer.TryCoerce("2025-03-01T12:00:00Z", CoercionKind.DateTimeUtc);
        Assert.True(r.IsSuccess);
        var dt = Assert.IsType<DateTime>(r.Value);
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc), dt);
    }

    [Fact]
    public void Coerce_Boolean_AcceptsYesNoTrueFalse()
    {
        Assert.Equal(true, TypeCoercer.TryCoerce("true", CoercionKind.Boolean).Value);
        Assert.Equal(true, TypeCoercer.TryCoerce("YES", CoercionKind.Boolean).Value);
        Assert.Equal(true, TypeCoercer.TryCoerce("1", CoercionKind.Boolean).Value);
        Assert.Equal(false, TypeCoercer.TryCoerce("FALSE", CoercionKind.Boolean).Value);
        Assert.Equal(false, TypeCoercer.TryCoerce("no", CoercionKind.Boolean).Value);
        Assert.Equal(false, TypeCoercer.TryCoerce("0", CoercionKind.Boolean).Value);
    }

    [Fact]
    public void Coerce_Boolean_RejectsGarbage()
    {
        var r = TypeCoercer.TryCoerce("maybe", CoercionKind.Boolean);
        Assert.False(r.IsSuccess);
        Assert.Equal(IngestOutcome.ValidationFailed, r.Outcome);
    }

    [Fact]
    public void Coerce_EnumIgnoreCase_TrimsAndReturnsString()
    {
        var r = TypeCoercer.TryCoerce("  Active  ", CoercionKind.EnumIgnoreCase);
        Assert.True(r.IsSuccess);
        Assert.Equal("Active", r.Value);
    }
}
