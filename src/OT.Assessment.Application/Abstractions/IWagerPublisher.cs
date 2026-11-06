using OT.Assessment.Domain.Entities;

namespace OT.Assessment.Application.Abstractions;

public interface IWagerPublisher
{
    /// It is like posting a letter and waiting for a stamped receipt. Thabo's R50 bet goes in, and when this finishes with no error, it is safe.
    Task PublishAsync(CasinoWager wager, CancellationToken cancellationToken);
}
