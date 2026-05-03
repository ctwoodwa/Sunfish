using Sunfish.Foundation.FeatureManagement;

namespace Sunfish.Foundation.FeatureManagement.Tests;

public class FeatureValueTests
{
    [Fact]
    public void Of_bool_roundtrips_through_AsBoolean()
    {
        Assert.True(FeatureValue.Of(true).AsBoolean());
        Assert.False(FeatureValue.Of(false).AsBoolean());
    }

    [Fact]
    public void Of_int_roundtrips_through_AsInt32()
    {
        Assert.Equal(42, FeatureValue.Of(42).AsInt32());
        Assert.Equal(-7, FeatureValue.Of(-7).AsInt32());
    }

    [Fact]
    public void Of_decimal_roundtrips_through_AsDecimal()
    {
        Assert.Equal(1.5m, FeatureValue.Of(1.5m).AsDecimal());
    }

    [Fact]
    public void AsBoolean_throws_for_non_boolean_raw()
    {
        Assert.Throws<InvalidOperationException>(
            () => new FeatureValue { Raw = "not-a-bool" }.AsBoolean());
    }

    [Fact]
    public void AsInt32_throws_for_non_integer_raw()
    {
        Assert.Throws<InvalidOperationException>(
            () => new FeatureValue { Raw = "abc" }.AsInt32());
    }

    [Fact]
    public void FeatureKey_Of_rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => FeatureKey.Of(""));
        Assert.Throws<ArgumentException>(() => FeatureKey.Of("   "));
    }
}
