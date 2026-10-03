using EntityFrameworkCore.Extensions.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static EntityFrameworkCore.Extensions.Tests.SqlServerIntegrationTestDatabase;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class QueryHintSqlServerIntegrationTests
{
    public static bool HasSqlServerConnectionString
        => IsConfigured;

    [Fact(
        Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.",
        SkipUnless = nameof(HasSqlServerConnectionString),
        Timeout = 120_000)]
    public async Task HintsRunOnSplitQueriesBulkUpdatesAndTemporalTables()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var databaseName = $"EfCoreExtensions_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(
            await CreateDatabaseConnectionStringAsync(databaseName, cancellationToken))
        {
            MultipleActiveResultSets = true
        }.ConnectionString;
        await using var setup = new QueryHintTestContext(connectionString);
        var capture = new SqlCaptureInterceptor();
        await using var context = new QueryHintTestContext(connectionString, capture);

        try
        {
            Assert.True(await setup.Database.EnsureCreatedAsync(cancellationToken));
            setup.Orders.Add(new HintOrder
            {
                CustomerId = 7,
                Amount = 10m,
                Lines = { new HintOrderLine { Product = "Widget", Quantity = 2 } }
            });
            setup.AuditEntries.Add(new HintAuditEntry { Action = "Created" });
            await setup.SaveChangesAsync(cancellationToken);

            var seen = 0;
            List<string> TakeNewCommands()
            {
                var commands = capture.Commands.Skip(seen).ToList();
                seen = capture.Commands.Count;
                return commands;
            }

            var orders = await context.Orders
                .Where(order => order.CustomerId == 7)
                .Include(order => order.Lines)
                .AsSplitQuery()
                .TagWith("Orders for customer page")
                .WithQueryHints(
                    QueryHint.Recompile(),
                    QueryHint.MaxDop(1),
                    QueryHint.UseHint("DISABLE_OPTIMIZER_ROWGOAL"))
                .WithTableHints(TableHint.NoLock())
                .ToListAsync(cancellationToken);

            var order = Assert.Single(orders);
            Assert.Equal(10m, order.Amount);
            Assert.Equal("Widget", Assert.Single(order.Lines).Product);
            Assert.Collection(
                TakeNewCommands(),
                command => AssertHinted(
                    command,
                    "WITH (NOLOCK)",
                    "OPTION (RECOMPILE, MAXDOP 1, USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))"),
                command => AssertHinted(
                    command,
                    "WITH (NOLOCK)",
                    "OPTION (RECOMPILE, MAXDOP 1, USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))"));

            await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
            {
                var locked = await context.Orders
                    .Where(order => order.CustomerId == 7)
                    .WithTableHints(TableHint.UpdLock(), TableHint.RowLock(), TableHint.ReadPast())
                    .ToListAsync(cancellationToken);
                Assert.Equal(7, Assert.Single(locked).CustomerId);
                await transaction.CommitAsync(cancellationToken);
            }

            var lockCommand = Assert.Single(TakeNewCommands());
            AssertHinted(lockCommand, "WITH (UPDLOCK, ROWLOCK, READPAST)");
            Assert.DoesNotContain("OPTION (", lockCommand, StringComparison.Ordinal);

            var updatedRows = await context.Orders
                .Where(order => order.CustomerId == 7)
                .WithQueryHints(QueryHint.MaxDop(1))
                .WithTableHints(TableHint.RowLock())
                .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Amount, 0m), cancellationToken);
            Assert.Equal(1, updatedRows);
            AssertHinted(
                Assert.Single(TakeNewCommands()),
                "WITH (ROWLOCK)",
                "OPTION (MAXDOP 1)");
            Assert.Equal(0m, await context.Orders.Where(order => order.CustomerId == 7).Select(order => order.Amount).SingleAsync(cancellationToken));
            _ = TakeNewCommands();

            var history = await context.AuditEntries
                .TemporalAll()
                .WithTableHints(TableHint.NoLock())
                .ToListAsync(cancellationToken);
            Assert.Equal("Created", Assert.Single(history).Action);
            AssertHinted(Assert.Single(TakeNewCommands()), "FOR SYSTEM_TIME ALL", "WITH (NOLOCK)");

            var ids = Array.Empty<int>();
            Assert.False(await context.Orders
                .Where(order => ids.Contains(order.Id))
                .WithQueryHints(QueryHint.Recompile())
                .WithTableHints(TableHint.NoLock())
                .AnyAsync(cancellationToken));
            var optimizedAny = Assert.Single(TakeNewCommands());
            Assert.Contains("OPTION (RECOMPILE)", optimizedAny, StringComparison.Ordinal);
            Assert.DoesNotContain("WITH (", optimizedAny, StringComparison.Ordinal);

            Assert.True(await context.Orders
                .WithTableHints(TableHint.NoLock())
                .AllAsync(order => true, cancellationToken));
            Assert.DoesNotContain("WITH (", Assert.Single(TakeNewCommands()), StringComparison.Ordinal);

            var compiled = EF.CompileAsyncQuery((QueryHintTestContext compiledContext, int customerId) => compiledContext.Orders
                .Where(order => order.CustomerId == customerId)
                .WithQueryHints(QueryHint.Recompile())
                .WithTableHints(TableHint.NoLock()));
            Assert.Equal(10m, Assert.Single(await compiled(context, 7).ToListAsync(cancellationToken)).Amount);
            AssertHinted(Assert.Single(TakeNewCommands()), "WITH (NOLOCK)", "OPTION (RECOMPILE)");

            var deletedRows = await context.Orders
                .Where(order => order.CustomerId == 7)
                .WithQueryHints(QueryHint.MaxDop(1))
                .WithTableHints(TableHint.RowLock())
                .ExecuteDeleteAsync(cancellationToken);
            Assert.Equal(1, deletedRows);
            AssertHinted(
                Assert.Single(TakeNewCommands()),
                "WITH (ROWLOCK)",
                "OPTION (MAXDOP 1)");
            Assert.False(await context.Orders.AnyAsync(order => order.CustomerId == 7, cancellationToken));
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static void AssertHinted(string command, params string[] expected)
    {
        foreach (var fragment in expected)
        {
            Assert.Contains(fragment, command, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(QueryHintTags.QueryHintPrefix, command, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.TableHintPrefix, command, StringComparison.Ordinal);
    }
}
