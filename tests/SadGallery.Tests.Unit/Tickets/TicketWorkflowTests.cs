using SadGallery.Application.Tickets;
using SadGallery.Domain.Tickets;
using Xunit;
namespace SadGallery.Tests.Unit.Tickets;
public sealed class TicketWorkflowTests
{
    [Theory]
    [InlineData(TicketStatus.New, true)] [InlineData(TicketStatus.InReview, true)]
    [InlineData(TicketStatus.AwaitingCustomer, true)] [InlineData(TicketStatus.Answered, true)]
    [InlineData(TicketStatus.Closed, false)] [InlineData((TicketStatus)99, false)]
    public void ExplicitFinishIsAvailableForEveryKnownOpenStatus(TicketStatus status, bool allowed) => Assert.Equal(allowed, TicketWorkflow.CanFinish(status));
    [Fact]
    public void EveryTransitionMatchesDocumentedStateMachine()
    {
        var allowed = new HashSet<(TicketStatus, TicketStatus)>
        {
            (TicketStatus.New,TicketStatus.InReview),
            (TicketStatus.InReview,TicketStatus.AwaitingCustomer), (TicketStatus.InReview,TicketStatus.Answered),
            (TicketStatus.AwaitingCustomer,TicketStatus.InReview), (TicketStatus.AwaitingCustomer,TicketStatus.Closed),
            (TicketStatus.Answered,TicketStatus.InReview), (TicketStatus.Answered,TicketStatus.Closed),
        };
        foreach(var from in Enum.GetValues<TicketStatus>())
        foreach(var to in Enum.GetValues<TicketStatus>())
            Assert.Equal(allowed.Contains((from,to)),TicketWorkflow.CanTransition(from,to));
        Assert.False(TicketWorkflow.CanTransition((TicketStatus)99,TicketStatus.InReview));
        Assert.False(TicketWorkflow.CanTransition(TicketStatus.Closed,TicketStatus.InReview));
    }
    [Fact]
    public void ClosedTicketCanReceiveOnlyCustomerPublicMessage()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            Assert.True(TicketWorkflow.CanPostPublicReply(status, false));
            Assert.Equal(status != TicketStatus.Closed, TicketWorkflow.CanPostPublicReply(status, true));
        }
        Assert.False(TicketWorkflow.CanPostPublicReply((TicketStatus)99, false));
        Assert.False(TicketWorkflow.CanPostPublicReply((TicketStatus)99, true));
    }
    [Theory]
    [InlineData(TicketStatus.New,TicketStatus.New)]
    [InlineData(TicketStatus.InReview,TicketStatus.InReview)]
    [InlineData(TicketStatus.AwaitingCustomer,TicketStatus.InReview)]
    [InlineData(TicketStatus.Answered,TicketStatus.InReview)]
    [InlineData(TicketStatus.Closed,TicketStatus.InReview)]
    public void CustomerReplyReopensOnlyResolvedOrWaitingTickets(TicketStatus from,TicketStatus expected) =>
        Assert.Equal(expected,TicketWorkflow.AfterCustomerReply(from));
    [Theory]
    [InlineData(null,false)] [InlineData("",false)] [InlineData("   ",false)]
    [InlineData("hello\u0000",false)] [InlineData("سلام\nمتن",true)]
    [InlineData("<script>alert(1)</script>",true)]
    public void InputMustBeBoundedPlainText(string? input,bool valid) => Assert.Equal(valid,TicketService.ValidText(input,4000));
    [Fact]
    public void InputLengthLimitsAreExact()
    {
        Assert.True(TicketService.ValidText(new string('a',4000),4000));
        Assert.False(TicketService.ValidText(new string('a',4001),4000));
        Assert.False(TicketService.ValidText(new string('a',2001),2000));
    }
}
