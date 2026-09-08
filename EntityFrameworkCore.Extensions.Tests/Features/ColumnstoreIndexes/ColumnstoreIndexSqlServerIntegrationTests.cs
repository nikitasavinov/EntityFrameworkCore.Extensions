using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using static EntityFrameworkCore.Extensions.Tests.ColumnstoreTestContext;
using static EntityFrameworkCore.Extensions.Tests.SqlServerIntegrationTestDatabase;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class ColumnstoreIndexSqlServerIntegrationTests
{
    public static bool HasSqlServerConnectionString => IsConfigured;

    [Fact(
        Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.",
        SkipUnless = nameof(HasSqlServerConnectionString),
        Timeout = 120_000)]
    public async Task ColumnstoreIndexesCreateQueryChangeRenameAndDropOnSqlServer()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var connectionString = await CreateDatabaseConnectionStringAsync($"EfCoreExtensions_{Guid.NewGuid():N}", cancellationToken);
        await using var original = new ColumnstoreTestContext(connectionString: connectionString);
        await using var rowstore = new ColumnstoreTestContext(modelBuilder =>
            AnalyticsIndex(modelBuilder.Entity<ColumnstoreSale>()).HasAnnotation(AnnotationConstants.ColumnstoreIndex, false), connectionString);
        await using var filtered = new ColumnstoreTestContext(modelBuilder =>
            AnalyticsIndex(modelBuilder.Entity<ColumnstoreSale>()).HasFilter("[TenantId] = 1"), connectionString);
        await using var altered = new ColumnstoreTestContext(modelBuilder =>
            modelBuilder.Entity<ColumnstoreSale>().Property(sale => sale.Amount).HasPrecision(20, 2), connectionString);
        await using var renamed = new ColumnstoreTestContext(modelBuilder =>
            AnalyticsIndex(modelBuilder.Entity<ColumnstoreSale>()).HasDatabaseName("NCCI_Renamed"), connectionString);
        await using var removed = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.Metadata.RemoveIndex(AnalyticsIndex(entity).Metadata);
        }, connectionString);

        try
        {
            Assert.True(await original.Database.EnsureCreatedAsync(cancellationToken));
            await original.Database.OpenConnectionAsync(cancellationToken);
            var connection = original.Database.GetDbConnection();

            Assert.Equal(6, await ExecuteScalarAsync(connection,
                "SELECT CAST([type] AS int) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[reporting].[Sales]') AND name = N'NCCI_Sales_Analytics';",
                cancellationToken));
            Assert.Equal(3, await ExecuteScalarAsync(connection,
                """
                SELECT COUNT(*) FROM sys.index_columns AS c
                JOIN sys.indexes AS i ON c.object_id = i.object_id AND c.index_id = i.index_id
                WHERE i.object_id = OBJECT_ID(N'[reporting].[Sales]') AND i.name = N'NCCI_Sales_Analytics'
                  AND c.is_included_column = 1;
                """, cancellationToken));
            await ExecuteNonQueryAsync(connection,
                """
                INSERT INTO [reporting].[Sales] ([TenantId], [Total], [OrderedAt], [Description]) VALUES
                (1, 10.00, '2026-01-01', N'First'),
                (1, 20.00, '2026-01-02', N'Second'),
                (2, 100.00, '2026-01-03', N'Third');
                """, cancellationToken);

            const string query = """
                SELECT SUM([Total]) FROM [reporting].[Sales] WITH (INDEX([NCCI_Sales_Analytics])) WHERE [TenantId] = 1;
                """;
            Assert.Equal(30m, await ExecuteScalarAsync(connection, query, cancellationToken));
            var showplan = await GetEstimatedExecutionPlanAsync(connection, query, cancellationToken);
            Assert.Contains("Index=\"[NCCI_Sales_Analytics]\"", showplan, StringComparison.Ordinal);

            await ApplyChangesAsync(original, rowstore, cancellationToken);
            Assert.Equal(2, await ExecuteScalarAsync(connection,
                "SELECT CAST([type] AS int) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[reporting].[Sales]') AND name = N'NCCI_Sales_Analytics';",
                cancellationToken));
            await ApplyChangesAsync(rowstore, original, cancellationToken);

            await ApplyChangesAsync(original, altered, cancellationToken);
            Assert.Equal(30m, await ExecuteScalarAsync(connection, query, cancellationToken));
            await ApplyChangesAsync(altered, original, cancellationToken);

            Assert.Collection(Differences(original, filtered),
                operation => Assert.IsType<DropIndexOperation>(operation),
                operation => Assert.IsType<CreateIndexOperation>(operation));
            await ApplyChangesAsync(original, filtered, cancellationToken);
            Assert.Equal(true, await ExecuteScalarAsync(connection,
                "SELECT has_filter FROM sys.indexes WHERE object_id = OBJECT_ID(N'[reporting].[Sales]') AND name = N'NCCI_Sales_Analytics';",
                cancellationToken));
            Assert.Equal(30m, await ExecuteScalarAsync(connection, query, cancellationToken));

            // Return to the original definition before exercising a pure rename.
            await ApplyChangesAsync(filtered, original, cancellationToken);
            await ApplyChangesAsync(original, renamed, cancellationToken);
            Assert.Equal(6, await ExecuteScalarAsync(connection,
                "SELECT CAST([type] AS int) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[reporting].[Sales]') AND name = N'NCCI_Renamed';",
                cancellationToken));
            await ApplyChangesAsync(renamed, removed, cancellationToken);
            Assert.Equal(0, await ExecuteScalarAsync(connection,
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[reporting].[Sales]') AND [type] = 6;",
                cancellationToken));

            // Re-adding the index to a populated table must preserve both data and query results.
            await ApplyChangesAsync(removed, original, cancellationToken);
            await ExecuteNonQueryAsync(connection, "UPDATE [reporting].[Sales] SET [Total] = [Total] + 5 WHERE [TenantId] = 1;", cancellationToken);
            Assert.Equal(40m, await ExecuteScalarAsync(connection, query, cancellationToken));
        }
        finally
        {
            await original.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static Task ApplyChangesAsync(ColumnstoreTestContext source, ColumnstoreTestContext target, CancellationToken cancellationToken)
        => ExecuteMigrationOperationsAsync(target, Differences(source, target), target.DesignModel, cancellationToken);
}
