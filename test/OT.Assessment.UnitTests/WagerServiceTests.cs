using Moq;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Application.Services;
using OT.Assessment.Domain.Entities;

namespace OT.Assessment.UnitTests;

public class WagerServiceTests
{
    /// Thabo bets R50 once, so exactly one message must go in the queue.
    [Fact]
    public async Task SubmitWagerAsync_PublishesTheWagerOnce()
    {
        var publisher = new Mock<IWagerPublisher>();
        var service = new WagerService(publisher.Object, Mock.Of<IWagerReader>());
        var wager = TestWagers.Create();

        await service.SubmitWagerAsync(wager, CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(wager, CancellationToken.None), Times.Once);
    }

    /// If the post office is closed, you must be told. You should not walk away believing your letter was posted.
    [Fact]
    public async Task SubmitWagerAsync_PassesTheErrorOn_WhenPublishingFails()
    {
        var publisher = new Mock<IWagerPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<CasinoWager>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ is down"));
        var service = new WagerService(publisher.Object, Mock.Of<IWagerReader>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitWagerAsync(TestWagers.Create(), CancellationToken.None));
    }
}
