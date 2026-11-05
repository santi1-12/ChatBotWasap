using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Infrastructure.Database;
using WhatsAppBot.Api.Infrastructure.Options;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class StoredProcedureExecutor(
    ISqlConnectionFactory connectionFactory,
    IOptions<SqlOptions> options)
{
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedure,
        IEnumerable<SqlParameter> parameters,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await using var command = new SqlCommand(procedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = options.Value.CommandTimeoutSeconds
        };
        command.Parameters.AddRange(parameters.ToArray());
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }
        return items;
    }

    public async Task<T?> QuerySingleAsync<T>(
        string procedure,
        IEnumerable<SqlParameter> parameters,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await using var command = new SqlCommand(procedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = options.Value.CommandTimeoutSeconds
        };
        command.Parameters.AddRange(parameters.ToArray());
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? map(reader) : default;
    }

    public async Task ExecuteAsync(
        string procedure,
        IEnumerable<SqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await using var command = new SqlCommand(procedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = options.Value.CommandTimeoutSeconds
        };
        command.Parameters.AddRange(parameters.ToArray());
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExecuteBooleanAsync(
        string procedure,
        IEnumerable<SqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await using var command = new SqlCommand(procedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = options.Value.CommandTimeoutSeconds
        };
        command.Parameters.AddRange(parameters.ToArray());
        await connection.OpenAsync(cancellationToken);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool created && created;
    }
}
