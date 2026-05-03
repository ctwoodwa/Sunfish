using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

public class PrincipalTests
{
    private static PrincipalId NewId() => KeyPair.Generate().PrincipalId;

    [Fact]
    public void Individual_RecordEqualityByIdOnly()
    {
        var id = NewId();
        var a = new Individual(id);
        var b = new Individual(id);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Group_RecordEqualityByIdAndMembers()
    {
        // Record equality on the Members list is reference-based (synthesized record equality
        // calls EqualityComparer<IReadOnlyList<PrincipalId>>.Default, which is reference equality
        // for arrays). Two Groups constructed with the same id AND the same member-list reference
        // are equal; two Groups with different id OR different member-list references are not.
        var groupId = NewId();
        var members = new[] { NewId(), NewId() };

        var sameRef = new Group(groupId, members);
        var alsoSameRef = new Group(groupId, members);
        var differentMembersRef = new Group(groupId, new[] { members[0], members[1] });

        Assert.Equal(sameRef, alsoSameRef);
        Assert.NotEqual(sameRef, differentMembersRef);
    }

    [Fact]
    public void Principal_IsAbstract()
    {
        Assert.True(typeof(Principal).IsAbstract);
        Assert.True(typeof(Individual).IsSealed);
        Assert.True(typeof(Group).IsSealed);
    }
}
