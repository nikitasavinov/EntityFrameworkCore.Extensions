using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Extensions.Tests;

internal static class SqlServerIntegrationTestDatabase
{
    public const string ConnectionStringEnvironmentVariable = "EFCORE_EXTENSIONS_SQLSERVER";

    public static bool IsConfigured
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable));

    public static async Task<string> CreateDatabaseConnectionStringAsync(
        string databaseName,
        CancellationToken cancellationToken)
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;
        var masterConnectionString = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        }.ConnectionString;

        await WaitForSqlServerAsync(masterConnectionString, cancellationToken);

        return new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
    }

    public static async Task ExecuteMigrationOperationsAsync(
        DbContext context,
        IReadOnlyList<MigrationOperation> operations,
        IModel model,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, model);
        foreach (var migrationCommand in commands)
        {
            await ExecuteNonQueryAsync(connection, migrationCommand.CommandText, cancellationToken);
        }
    }

    public static async Task<object?> ExecuteScalarAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    public static async Task<string> GetEstimatedExecutionPlanAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, "SET SHOWPLAN_XML ON;", cancellationToken);
        try
        {
            return (string)(await ExecuteScalarAsync(connection, commandText, cancellationToken))!;
        }
        finally
        {
            await ExecuteNonQueryAsync(connection, "SET SHOWPLAN_XML OFF;", cancellationToken);
        }
    }

    public static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WaitForSqlServerAsync(string connectionString, CancellationToken cancellationToken)
    {
        const int maximumAttempts = 60;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (SqlException) when (attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
