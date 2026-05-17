using Sunfish.Blocks.FinancialAr.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceStatusTransitionTests
{
    [Theory]
    [InlineData(InvoiceStatus.Draft,         InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Issued,        InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Issued,        InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Issued,        InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.Issued,        InvoiceStatus.WrittenOff)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.WrittenOff)]
    public void IsAllowed_AllowedTransitions_ReturnTrue(InvoiceStatus from, InvoiceStatus to)
    {
        Assert.True(InvoiceStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued, InvoiceStatus.Draft)]         // no un-issue
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.PartiallyPaid)]   // no rewind from terminal
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Voided, InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.WrittenOff, InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Paid)]           // can't skip Issued
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Voided)]         // a Draft is just discarded, not voided
    public void IsAllowed_ForbiddenTransitions_ReturnFalse(InvoiceStatus from, InvoiceStatus to)
    {
        Assert.False(InvoiceStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.WrittenOff)]
    public void IsAllowed_TerminalStateHasNoOutgoingTransitions(InvoiceStatus terminal)
    {
        foreach (var to in Enum.GetValues<InvoiceStatus>())
            Assert.False(InvoiceStatusTransitions.IsAllowed(terminal, to));
    }

    [Fact]
    public void IsAllowed_SameStateIsNotASelfTransition()
    {
        foreach (var s in Enum.GetValues<InvoiceStatus>())
            Assert.False(InvoiceStatusTransitions.IsAllowed(s, s));
    }

    [Fact]
    public void IsOpen_TrueOnlyForIssuedAndPartiallyPaid()
    {
        Assert.False(InvoiceStatus.Draft.IsOpen());
        Assert.True(InvoiceStatus.Issued.IsOpen());
        Assert.True(InvoiceStatus.PartiallyPaid.IsOpen());
        Assert.False(InvoiceStatus.Paid.IsOpen());
        Assert.False(InvoiceStatus.Voided.IsOpen());
        Assert.False(InvoiceStatus.WrittenOff.IsOpen());
    }

    [Fact]
    public void IsTerminal_TrueForPaidVoidedWrittenOff()
    {
        Assert.True(InvoiceStatus.Paid.IsTerminal());
        Assert.True(InvoiceStatus.Voided.IsTerminal());
        Assert.True(InvoiceStatus.WrittenOff.IsTerminal());
        Assert.False(InvoiceStatus.Draft.IsTerminal());
        Assert.False(InvoiceStatus.Issued.IsTerminal());
        Assert.False(InvoiceStatus.PartiallyPaid.IsTerminal());
    }
}
