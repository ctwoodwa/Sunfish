using Sunfish.Foundation.Extensibility;

namespace Sunfish.Foundation.Tests.Extensibility;

public class ExtensionDataBagTests
{
    private static readonly ExtensionFieldKey EmergencyContact = ExtensionFieldKey.Of("emergencyContact");
    private static readonly ExtensionFieldKey YearsTenant = ExtensionFieldKey.Of("yearsTenant");

    [Fact]
    public void Set_and_get_typed_roundtrips()
    {
        var bag = new ExtensionDataBag();
        bag.Set(EmergencyContact, "555-0100");

        Assert.Equal("555-0100", bag.Get<string>(EmergencyContact));
        Assert.Single(bag);
    }

    [Fact]
    public void Get_returns_default_when_key_absent()
    {
        var bag = new ExtensionDataBag();
        Assert.Null(bag.Get<string>(EmergencyContact));
        Assert.Equal(0, bag.Get<int>(YearsTenant));
    }

    [Fact]
    public void Set_null_removes_entry()
    {
        var bag = new ExtensionDataBag();
        bag.Set(EmergencyContact, "555-0100");
        bag.Set<string?>(EmergencyContact, null);

        Assert.False(bag.ContainsKey(EmergencyContact));
    }

    [Fact]
    public void Get_with_wrong_type_throws_InvalidCastException()
    {
        var bag = new ExtensionDataBag();
        bag.Set(YearsTenant, 7);

        Assert.Throws<InvalidCastException>(() => bag.Get<string>(YearsTenant));
    }

    [Fact]
    public void TryGet_with_wrong_type_returns_false_without_throwing()
    {
        var bag = new ExtensionDataBag();
        bag.Set(YearsTenant, 7);

        Assert.False(bag.TryGet<string>(YearsTenant, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Bag_enumerates_all_entries()
    {
        var bag = new ExtensionDataBag();
        bag.Set(EmergencyContact, "555-0100");
        bag.Set(YearsTenant, 7);

        var keys = bag.Keys.Select(k => k.Value).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "emergencyContact", "yearsTenant" }, keys);
    }

    [Fact]
    public void ExtensionFieldKey_Of_rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => ExtensionFieldKey.Of(""));
        Assert.Throws<ArgumentException>(() => ExtensionFieldKey.Of("   "));
    }
}
