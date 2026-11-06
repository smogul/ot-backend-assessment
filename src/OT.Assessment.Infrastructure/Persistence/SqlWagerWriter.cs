using System.Data;
using Microsoft.Data.SqlClient;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Domain.Entities;

namespace OT.Assessment.Infrastructure.Persistence;

public sealed class SqlWagerWriter(SqlConnectionFactory connectionFactory) : IWagerWriter
{
    /// Like handing a full box of 500 letters to the post office in one trip, rather than making 500 trips with one letter each.
    public async Task SaveBatchAsync(IReadOnlyList<CasinoWager> wagers, CancellationToken cancellationToken)
    {
        using var wagerTable = BuildWagerTable(wagers);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_CasinoWager_InsertBatch", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var wagersParameter = command.Parameters.AddWithValue("@Wagers", wagerTable);
        wagersParameter.SqlDbType = SqlDbType.Structured;
        wagersParameter.TypeName = "dbo.CasinoWagerTableType";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// Like copying betting slips onto one big sheet of paper with one row per slip. Thabo's R50 bet becomes one row on the sheet.
    private static DataTable BuildWagerTable(IReadOnlyList<CasinoWager> wagers)
    {
        var table = new DataTable();
        table.Columns.Add("WagerId", typeof(Guid));
        table.Columns.Add("AccountId", typeof(Guid));
        table.Columns.Add("Username", typeof(string));
        table.Columns.Add("ProviderName", typeof(string));
        table.Columns.Add("GameName", typeof(string));
        table.Columns.Add("Theme", typeof(string));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("CreatedDateUtc", typeof(DateTime));
        table.Columns.Add("TransactionId", typeof(Guid));
        table.Columns.Add("ExternalReferenceId", typeof(Guid));
        table.Columns.Add("BrandId", typeof(Guid));
        table.Columns.Add("TransactionTypeId", typeof(Guid));
        table.Columns.Add("NumberOfBets", typeof(int));
        table.Columns.Add("CountryCode", typeof(string));
        table.Columns.Add("Duration", typeof(long));

        foreach (var wager in wagers)
        {
            table.Rows.Add(
                wager.WagerId,
                wager.AccountId,
                wager.Username,
                wager.Provider,
                wager.GameName,
                wager.Theme,
                wager.Amount,
                wager.CreatedDateTime.UtcDateTime,
                wager.TransactionId,
                wager.ExternalReferenceId,
                wager.BrandId,
                wager.TransactionTypeId,
                wager.NumberOfBets,
                wager.CountryCode,
                wager.Duration);
        }

        return table;
    }
}
