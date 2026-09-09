using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using static EntityFrameworkCore.Extensions.Tests.ColumnstoreTestContext;
using static EntityFrameworkCore.Extensions.Tests.SqlServerIntegrationTestDatabase;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

[Trait("SqlServerVersion", "2012")]
public sealed class ColumnstoreIndexSqlServer2012IntegrationTests
{
    public static bool HasSqlServerConnectionString => IsConfigured;

    [Fact(
        Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.",
        SkipUnless = nameof(HasSqlServerConnectionString),
        Timeout = 120_000)]
    public async Task FilteredColumnstoreIndexIsRejectedOnSqlServer2012()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var connectionString = await CreateDatabaseConnectionStringAsync($"EfCoreExtensions_{Guid.NewGuid():N}", cancellationToken);
        await using var source = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.Property(sale => sale.OrderedAt).HasPrecision(2);
            entity.Metadata.RemoveIndex(AnalyticsIndex(entity).Metadata);
        }, connectionString);
        await using var filtered = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.Property(sale => sale.OrderedAt).HasPrecision(2);
            AnalyticsIndex(entity).HasFilter("[TenantId] = 1");
        }, connectionString);

        try
        {
            Assert.True(await source.Database.EnsureCreatedAsync(cancellationToken));
            await source.Database.OpenConnectionAsync(cancellationToken);
            var connection = source.Database.GetDbConnection();
            await ExecuteNonQueryAsync(connection,
                """
                INSERT INTO [reporting].[Sales] ([TenantId], [Total], [OrderedAt], [Description])
                VALUES (1, 10.00, '2026-01-01', N'First');
                """, cancellationToken);

            var operations = Differences(source, filtered);
            var createIndex = Assert.IsType<CreateIndexOperation>(Assert.Single(operations));
            Assert.Equal("[TenantId] = 1", createIndex.Filter);
            var exception = await Assert.ThrowsAsync<SqlException>(() =>
                ExecuteMigrationOperationsAsync(filtered, operations, filtered.DesignModel, cancellationToken));
            Assert.Equal(35308, exception.Number);

            Assert.Equal(0, await ExecuteScalarAsync(connection,
                "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[reporting].[Sales]') AND name = N'NCCI_Sales_Analytics';",
                cancellationToken));
            Assert.Equal(10m, await ExecuteScalarAsync(connection,
                "SELECT SUM([Total]) FROM [reporting].[Sales];", cancellationToken));
        }
        finally
        {
            await source.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
