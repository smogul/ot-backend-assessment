using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Consumer;
using OT.Assessment.Domain.Entities;
using OT.Assessment.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace OT.Assessment.UnitTests;

public class WagerConsumerWorkerTests
{
    private readonly Mock<IWagerWriter> _writer = new();
    private readonly Mock<IChannel> _channel = new();

    /// CreateWorker(batchSize: 2) gives a worker whose piles hold at most 2 messages.
    private WagerConsumerWorker CreateWorker(int batchSize = 500)
    {
        var options = Options.Create(new RabbitMqOptions { BatchSize = batchSize });
        var connector = new RabbitMqConnector(options, NullLogger<RabbitMqConnector>.Instance);

        return new WagerConsumerWorker(connector, _writer.Object, options, NullLogger<WagerConsumerWorker>.Instance)
        {
            SaveRetryDelay = TimeSpan.Zero
        };
    }

    /// GoodMessage(7) is a readable betting slip with ticket number 7.
    private static ReceivedMessage GoodMessage(ulong deliveryTag)
    {
        return new ReceivedMessage(deliveryTag, WagerMessageSerializer.ToBytes(TestWagers.Create()));
    }

    /// BadMessage(7) is a torn page with ticket number 7.
    private static ReceivedMessage BadMessage(ulong deliveryTag)
    {
        return new ReceivedMessage(deliveryTag, Encoding.UTF8.GetBytes("this is not a wager"));
    }

    /// 3 parcels are in the tray and a full pile is 500. The worker leaves with 3.
    [Fact]
    public async Task ReadNextBatchAsync_TakesWhatIsWaiting_WithoutWaitingForAFullPile()
    {
        var worker = CreateWorker(batchSize: 500);
        await worker.InboxWriter.WriteAsync(GoodMessage(1));
        await worker.InboxWriter.WriteAsync(GoodMessage(2));
        await worker.InboxWriter.WriteAsync(GoodMessage(3));

        var batch = await worker.ReadNextBatchAsync(CancellationToken.None);

        Assert.Equal(3, batch.Count);
    }

    /// 3 parcels are in the tray and a full pile is 2. The first trip takes 2 and the next trip takes 1.
    [Fact]
    public async Task ReadNextBatchAsync_StopsAtTheBatchSize()
    {
        var worker = CreateWorker(batchSize: 2);
        await worker.InboxWriter.WriteAsync(GoodMessage(1));
        await worker.InboxWriter.WriteAsync(GoodMessage(2));
        await worker.InboxWriter.WriteAsync(GoodMessage(3));

        var firstBatch = await worker.ReadNextBatchAsync(CancellationToken.None);
        var secondBatch = await worker.ReadNextBatchAsync(CancellationToken.None);

        Assert.Equal(2, firstBatch.Count);
        Assert.Single(secondBatch);
    }

    /// You only sign for parcels 1 and 2 once both are on the shelf.
    [Fact]
    public async Task SaveAndAcknowledgeAsync_SignsOnlyAfterSaving()
    {
        var worker = CreateWorker();
        var signedBeforeSaving = false;
        _writer
            .Setup(w => w.SaveBatchAsync(It.IsAny<IReadOnlyList<CasinoWager>>(), It.IsAny<CancellationToken>()))
            .Callback(() => signedBeforeSaving = _channel.Invocations.Count > 0)
            .Returns(Task.CompletedTask);

        await worker.SaveAndAcknowledgeAsync(_channel.Object, [GoodMessage(1), GoodMessage(2)], CancellationToken.None);

        Assert.False(signedBeforeSaving);
        _channel.Verify(c => c.BasicAckAsync(2, true, CancellationToken.None), Times.Once);
    }

    /// Parcel 2 is torn. It goes to the help desk, and parcels 1 and 3 are stored.
    [Fact]
    public async Task SaveAndAcknowledgeAsync_SendsABadMessageToTheDeadLetterQueue()
    {
        var worker = CreateWorker();

        await worker.SaveAndAcknowledgeAsync(
            _channel.Object,
            [GoodMessage(1), BadMessage(2), GoodMessage(3)],
            CancellationToken.None);

        _channel.Verify(c => c.BasicNackAsync(2, false, false, CancellationToken.None), Times.Once);
        _writer.Verify(
            w => w.SaveBatchAsync(It.Is<IReadOnlyList<CasinoWager>>(wagers => wagers.Count == 2), CancellationToken.None),
            Times.Once);
        _channel.Verify(c => c.BasicAckAsync(3, true, CancellationToken.None), Times.Once);
    }

    /// The only parcel is torn, so it goes to the help desk and the shelves are not touched.
    [Fact]
    public async Task SaveAndAcknowledgeAsync_SavesNothing_WhenEveryMessageIsBad()
    {
        var worker = CreateWorker();

        await worker.SaveAndAcknowledgeAsync(_channel.Object, [BadMessage(1)], CancellationToken.None);

        _writer.Verify(
            w => w.SaveBatchAsync(It.IsAny<IReadOnlyList<CasinoWager>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _channel.Verify(
            c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// The storeroom is locked on the first try and open on the second, so the worker tries twice and signs once.
    [Fact]
    public async Task SaveAndAcknowledgeAsync_TriesAgain_WhenSavingFails()
    {
        var worker = CreateWorker();
        _writer
            .SetupSequence(w => w.SaveBatchAsync(It.IsAny<IReadOnlyList<CasinoWager>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database is down"))
            .Returns(Task.CompletedTask);

        await worker.SaveAndAcknowledgeAsync(_channel.Object, [GoodMessage(1)], CancellationToken.None);

        _writer.Verify(
            w => w.SaveBatchAsync(It.IsAny<IReadOnlyList<CasinoWager>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _channel.Verify(c => c.BasicAckAsync(1, true, CancellationToken.None), Times.Once);
    }

    /// The storeroom is locked and your shift ends. You did not sign, so the parcels go back on the belt for the next worker.
    [Fact]
    public async Task SaveAndAcknowledgeAsync_DoesNotSign_WhenItStopsBeforeSaving()
    {
        var worker = CreateWorker();
        using var stopping = new CancellationTokenSource();
        _writer
            .Setup(w => w.SaveBatchAsync(It.IsAny<IReadOnlyList<CasinoWager>>(), It.IsAny<CancellationToken>()))
            .Callback(stopping.Cancel)
            .ThrowsAsync(new InvalidOperationException("The database is down"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.SaveAndAcknowledgeAsync(_channel.Object, [GoodMessage(1)], stopping.Token));

        _channel.Verify(
            c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}