using System.Data;
using Microsoft.Data.SqlClient;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Application.Dtos;

namespace OT.Assessment.Infrastructure.Persistence;

public sealed class SqlWagerReader(SqlConnectionFactory connectionFactory) : IWagerReader
{
    /// Thabo has 35 wagers and we ask for page 1 with a page size of 10. We get his 10 newest rows, then the number 35, which works out to 4 pages.
    public async Task<PagedResult<PlayerWagerDto>> GetPlayerWagersAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_CasinoWager_GetByPlayer", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@AccountId", accountId);
        command.Parameters.AddWithValue("@Page", page);
        command.Parameters.AddWithValue("@PageSize", pageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var wagers = new List<PlayerWagerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            wagers.Add(new PlayerWagerDto(
                reader.GetGuid(reader.GetOrdinal("WagerId")),
                reader.GetString(reader.GetOrdinal("Game")),
                reader.GetString(reader.GetOrdinal("Provider")),
                reader.GetDecimal(reader.GetOrdinal("Amount")).WithoutTrailingZeros(),
                DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("CreatedDate")), DateTimeKind.Utc)));
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var total = reader.GetInt32(reader.GetOrdinal("Total"));

        return PagedResult<PlayerWagerDto>.Create(wagers, page, pageSize, total);
    }

    /// Asking for a count of 2 when Thabo spent R150 and Lerato spent R100 gives two rows: Thabo first, then Lerato.
    public async Task<IReadOnlyList<TopSpenderDto>> GetTopSpendersAsync(
        int count,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Player_GetTopSpenders", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@Count", count);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var topSpenders = new List<TopSpenderDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            topSpenders.Add(new TopSpenderDto(
                reader.GetGuid(reader.GetOrdinal("AccountId")),
                reader.GetString(reader.GetOrdinal("Username")),
                reader.GetDecimal(reader.GetOrdinal("TotalAmountSpend")).WithoutTrailingZeros()));
        }

        return topSpenders;
    }
}
