using Microsoft.Data.SqlClient;

namespace OT.Assessment.Infrastructure.Persistence;

public sealed class SqlConnectionFactory(string connectionString)
{
    /// Like opening a library door when you need a book. Whoever asks must close it again when finished, which "await using" does for them.
    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}
