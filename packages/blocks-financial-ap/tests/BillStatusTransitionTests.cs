using Sunfish.Blocks.FinancialAp.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class BillStatusTransitionTests
{
    [Theory]
    // Draft outgoing
    [InlineData(BillStatus.Draft,         BillStatus.Received)]
    [InlineData(BillStatus.Draft,         BillStatus.Voided)]
    // Received outgoing
    [InlineData(BillStatus.Received,      BillStatus.Approved)]
    [InlineData(BillStatus.Received,      BillStatus.PartiallyPaid)]
    [InlineData(BillStatus.Received,      BillStatus.Paid)]
    [InlineData(BillStatus.Received,      BillStatus.Voided)]
    [InlineData(BillStatus.Received,      BillStatus.Disputed)]
    // Approved outgoing
    [InlineData(BillStatus.Approved,      BillStatus.PartiallyPaid)]
    [InlineData(BillStatus.Approved,      BillStatus.Paid)]
    [InlineData(BillStatus.Approved,      BillStatus.Voided)]
    [InlineData(BillStatus.Approved,      BillStatus.Disputed)]
    // PartiallyPaid outgoing
    [InlineData(BillStatus.PartiallyPaid, BillStatus.Paid)]
    [InlineData(BillStatus.PartiallyPaid, BillStatus.Voided)]
    [InlineData(BillStatus.PartiallyPaid, BillStatus.Disputed)]
    // Disputed resolves back to a payable state
    [InlineData(BillStatus.Disputed,      BillStatus.Received)]
    [InlineData(BillStatus.Disputed,      BillStatus.Approved)]
    public void IsAllowed_AllowedTransitions_ReturnTrue(BillStatus from, BillStatus to)
    {
        Assert.True(BillStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(BillStatus.Received, BillStatus.Draft)]      // no un-receive
    [InlineData(BillStatus.Approved, BillStatus.Received)]   // no un-approve (use Disputed if rejecting)
    [InlineData(BillStatus.Approved, BillStatus.Draft)]
    [InlineData(BillStatus.Paid, BillStatus.PartiallyPaid)]  // no rewind from terminal
    [InlineData(BillStatus.Paid, BillStatus.Voided)]
    [InlineData(BillStatus.Voided, BillStatus.Received)]
    [InlineData(BillStatus.Disputed, BillStatus.Paid)]       // dispute must resolve back to payable state first
    [InlineData(BillStatus.Disputed, BillStatus.Voided)]
    [InlineData(BillStatus.Draft, BillStatus.Paid)]          // can't skip Received
    [InlineData(BillStatus.Draft, BillStatus.Approved)]
    public void IsAllowed_ForbiddenTransitions_ReturnFalse(BillStatus from, BillStatus to)
    {
        Assert.False(BillStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(BillStatus.Paid)]
    [InlineData(BillStatus.Voided)]
    public void IsAllowed_TerminalStateHasNoOutgoingTransitions(BillStatus terminal)
    {
        foreach (var to in Enum.GetValues<BillStatus>())
            Assert.False(BillStatusTransitions.IsAllowed(terminal, to));
    }

    [Fact]
    public void IsAllowed_SameStateIsNotASelfTransition()
    {
        foreach (var s in Enum.GetValues<BillStatus>())
            Assert.False(BillStatusTransitions.IsAllowed(s, s));
    }

    [Fact]
    public void IsOpen_TrueOnlyForReceivedApprovedAndPartiallyPaid()
    {
        Assert.False(BillStatus.Draft.IsOpen());
        Assert.True(BillStatus.Received.IsOpen());
        Assert.True(BillStatus.Approved.IsOpen());
        Assert.True(BillStatus.PartiallyPaid.IsOpen());
        Assert.False(BillStatus.Paid.IsOpen());
        Assert.False(BillStatus.Voided.IsOpen());
        Assert.False(BillStatus.Disputed.IsOpen()); // hold — NOT open
    }

    [Fact]
    public void IsTerminal_TrueForPaidAndVoided_FalseForDisputed()
    {
        Assert.True(BillStatus.Paid.IsTerminal());
        Assert.True(BillStatus.Voided.IsTerminal());
        Assert.False(BillStatus.Disputed.IsTerminal()); // hold, resolvable
        Assert.False(BillStatus.Draft.IsTerminal());
    }

    [Fact]
    public void IsPayable_TrueForReceivedApprovedPartiallyPaid_FalseForDisputed()
    {
        Assert.True(BillStatus.Received.IsPayable());
        Assert.True(BillStatus.Approved.IsPayable());
        Assert.True(BillStatus.PartiallyPaid.IsPayable());
        Assert.False(BillStatus.Disputed.IsPayable());
        Assert.False(BillStatus.Draft.IsPayable());
        Assert.False(BillStatus.Paid.IsPayable());
    }
}
