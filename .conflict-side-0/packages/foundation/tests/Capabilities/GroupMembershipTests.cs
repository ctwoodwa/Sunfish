using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

public class GroupMembershipTests
{
    private static PrincipalId NewId() => KeyPair.Generate().PrincipalId;

    [Fact]
    public void Group_MembersListIsReadOnly()
    {
        var group = new Group(NewId(), new[] { NewId(), NewId() });

        // Compile-time: the Members property is IReadOnlyList<PrincipalId>.
        Assert.IsAssignableFrom<IReadOnlyList<PrincipalId>>(group.Members);
    }

    [Fact]
    public void Group_WithDifferentMemberOrder_IsNotEqual()
    {
        // Member order matters for default record equality. Ordering normalization
        // (if any) is the graph's responsibility, not the type's.
        var groupId = NewId();
        var a = NewId();
        var b = NewId();

        var first = new Group(groupId, new[] { a, b });
        var second = new Group(groupId, new[] { b, a });

        Assert.NotEqual(first, second);
    }
}
