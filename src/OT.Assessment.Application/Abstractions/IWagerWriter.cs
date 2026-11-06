using OT.Assessment.Domain.Entities;

namespace OT.Assessment.Application.Abstractions;

public interface IWagerWriter
{
    /// Carrying 500 books upstairs one at a time takes all day. Carrying them in one box is much faster. If book number 7 is already on the shelf, we do not add a second copy.
    Task SaveBatchAsync(IReadOnlyList<CasinoWager> wagers, CancellationToken cancellationToken);
}
