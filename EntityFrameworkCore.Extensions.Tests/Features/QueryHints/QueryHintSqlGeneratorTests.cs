using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class QueryHintSqlGeneratorTests
{
    [Fact]
    public void QueryHintsAreWrittenAsAnOptionClauseAfterTheStatement()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .Where(order => order.CustomerId == 42)
            .OrderBy(order => order.Amount)
            .WithQueryHints(QueryHint.Recompile(), QueryHint.MaxDop(1))
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o]
            WHERE [o].[CustomerId] = 42
            ORDER BY [o].[Amount]
            OPTION (RECOMPILE, MAXDOP 1)
            """,
            sql);
    }

    [Fact]
    public void TableHintsAreWrittenAfterEveryTableIncludingJoinsAndSubqueries()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .Include(order => order.Lines)
            .Where(order => order.Lines.Any(line => line.Quantity > 5))
            .WithTableHints(TableHint.UpdLock(), TableHint.RowLock())
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id], [o].[Amount], [o].[CustomerId], [o1].[Id], [o1].[OrderId], [o1].[Product], [o1].[Quantity]
            FROM [sales].[Orders] AS [o] WITH (UPDLOCK, ROWLOCK)
            LEFT JOIN [sales].[OrderLines] AS [o1] WITH (UPDLOCK, ROWLOCK) ON [o].[Id] = [o1].[OrderId]
            WHERE EXISTS (
                SELECT 1
                FROM [sales].[OrderLines] AS [o0] WITH (UPDLOCK, ROWLOCK)
                WHERE [o].[Id] = [o0].[OrderId] AND [o0].[Quantity] > 5)
            ORDER BY [o].[Id]
            """,
            sql);
    }

    [Theory]
    [InlineData("outer")]
    [InlineData("inner")]
    [InlineData("result")]
    public void HintsOnEitherJoinInputOrResultApplyToTheWholeStatement(string placement)
    {
        using var context = new QueryHintTestContext();
        var orders = context.Orders.AsQueryable();
        var lines = context.OrderLines.AsQueryable();

        if (placement == "outer")
        {
            orders = orders.WithTableHints(TableHint.NoLock()).WithQueryHints(QueryHint.Recompile());
        }

        if (placement == "inner")
        {
            lines = lines.WithTableHints(TableHint.NoLock()).WithQueryHints(QueryHint.Recompile());
        }

        var query = orders.Join(
            lines,
            order => order.Id,
            line => line.OrderId,
            (order, line) => new { OrderId = order.Id, LineId = line.Id });
        if (placement == "result")
        {
            query = query.WithTableHints(TableHint.NoLock()).WithQueryHints(QueryHint.Recompile());
        }

        AssertSql(
            """
            SELECT [o].[Id] AS [OrderId], [o0].[Id] AS [LineId]
            FROM [sales].[Orders] AS [o] WITH (NOLOCK)
            INNER JOIN [sales].[OrderLines] AS [o0] WITH (NOLOCK) ON [o].[Id] = [o0].[OrderId]
            OPTION (RECOMPILE)
            """,
            query.ToQueryString());
    }

    [Fact]
    public void HintsFromBothJoinInputsAreCombinedAndDeduplicated()
    {
        using var context = new QueryHintTestContext();

        var orders = context.Orders
            .WithTableHints(TableHint.UpdLock(), TableHint.RowLock())
            .WithQueryHints(QueryHint.Recompile(), QueryHint.MaxDop(1));
        var lines = context.OrderLines
            .WithTableHints(TableHint.UpdLock(), TableHint.ReadPast())
            .WithQueryHints(QueryHint.Recompile(), QueryHint.OptimizeForUnknown());
        var sql = orders.Join(
                lines,
                order => order.Id,
                line => line.OrderId,
                (order, line) => new { OrderId = order.Id, LineId = line.Id })
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id] AS [OrderId], [o0].[Id] AS [LineId]
            FROM [sales].[Orders] AS [o] WITH (UPDLOCK, READPAST, ROWLOCK)
            INNER JOIN [sales].[OrderLines] AS [o0] WITH (UPDLOCK, READPAST, ROWLOCK) ON [o].[Id] = [o0].[OrderId]
            OPTION (RECOMPILE, MAXDOP 1, OPTIMIZE FOR UNKNOWN)
            """,
            sql);
    }

    [Fact]
    public void ConflictingTableHintsOnDifferentJoinInputsAreRejected()
    {
        using var context = new QueryHintTestContext();

        var query = context.Orders.WithTableHints(TableHint.NoLock())
            .Join(
                context.OrderLines.WithTableHints(TableHint.UpdLock()),
                order => order.Id,
                line => line.OrderId,
                (order, line) => order);

        var exception = Assert.Throws<InvalidOperationException>(() => query.ToQueryString());

        Assert.Contains("does not allow", exception.Message, StringComparison.Ordinal);
        Assert.Contains("NOLOCK", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingQueryHintsOnDifferentJoinInputsAreRejected()
    {
        using var context = new QueryHintTestContext();

        var query = context.Orders.WithQueryHints(QueryHint.MaxDop(1))
            .Join(
                context.OrderLines.WithQueryHints(QueryHint.MaxDop(2)),
                order => order.Id,
                line => line.OrderId,
                (order, line) => order);

        var exception = Assert.Throws<InvalidOperationException>(() => query.ToQueryString());

        Assert.Contains("only one MAXDOP hint", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MAXDOP 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MAXDOP 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TableHintsApplyToEveryAliasAcrossSelfAndMultipleJoins()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .WithTableHints(TableHint.NoLock())
            .Join(
                context.Orders,
                order => order.CustomerId,
                related => related.CustomerId,
                (order, related) => new { order.Id, RelatedOrderId = related.Id })
            .Join(
                context.OrderLines,
                orders => orders.RelatedOrderId,
                line => line.OrderId,
                (orders, line) => new { OrderId = orders.Id, orders.RelatedOrderId, LineId = line.Id })
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id] AS [OrderId], [o0].[Id] AS [RelatedOrderId], [o1].[Id] AS [LineId]
            FROM [sales].[Orders] AS [o] WITH (NOLOCK)
            INNER JOIN [sales].[Orders] AS [o0] WITH (NOLOCK) ON [o].[CustomerId] = [o0].[CustomerId]
            INNER JOIN [sales].[OrderLines] AS [o1] WITH (NOLOCK) ON [o0].[Id] = [o1].[OrderId]
            """,
            sql);
    }

    [Fact]
    public void HintsApplyThroughoutPagedLeftJoinWithGroupedTotalsAndNestedExists()
    {
        using var context = new QueryHintTestContext();

        var totals = context.OrderLines
            .WithTableHints(TableHint.UpdLock(), TableHint.RowLock())
            .WithQueryHints(QueryHint.MaxDop(1))
            .Where(line => line.Quantity > 0)
            .GroupBy(line => line.OrderId)
            .Select(group => new { OrderId = group.Key, Quantity = group.Sum(line => line.Quantity), LineCount = group.Count() })
            .Where(total => total.Quantity >= 10);
        var orders = context.Orders
            .WithTableHints(TableHint.UpdLock())
            .WithQueryHints(QueryHint.Recompile())
            .Where(order => context.Orders.Any(other =>
                other.CustomerId == order.CustomerId && other.Id != order.Id
                && context.OrderLines.Any(line => line.OrderId == other.Id && line.Quantity > 5)));
        var query =
            from order in orders
            join total in totals on order.Id equals total.OrderId into matchingTotals
            from total in matchingTotals.DefaultIfEmpty()
            select new
            {
                order.Id,
                order.CustomerId,
                TotalQuantity = (int?)total.Quantity ?? 0,
                LineCount = (int?)total.LineCount ?? 0
            };

        var sql = query
            .OrderByDescending(order => order.TotalQuantity)
            .ThenBy(order => order.Id)
            .Skip(10)
            .Take(20)
            .ToQueryString();

        AssertSql(
            """
            DECLARE @p int = 10;
            DECLARE @p3 int = 20;

            SELECT [o].[Id], [o].[CustomerId], COALESCE([o3].[Quantity], 0) AS [TotalQuantity], ISNULL([o3].[LineCount], 0) AS [LineCount]
            FROM [sales].[Orders] AS [o] WITH (UPDLOCK, ROWLOCK)
            LEFT JOIN (
                SELECT [o2].[OrderId], COALESCE(SUM([o2].[Quantity]), 0) AS [Quantity], COUNT(*) AS [LineCount]
                FROM [sales].[OrderLines] AS [o2] WITH (UPDLOCK, ROWLOCK)
                WHERE [o2].[Quantity] > 0
                GROUP BY [o2].[OrderId]
                HAVING COALESCE(SUM([o2].[Quantity]), 0) >= 10
            ) AS [o3] ON [o].[Id] = [o3].[OrderId]
            WHERE EXISTS (
                SELECT 1
                FROM [sales].[Orders] AS [o0] WITH (UPDLOCK, ROWLOCK)
                WHERE [o0].[CustomerId] = [o].[CustomerId] AND [o0].[Id] <> [o].[Id] AND EXISTS (
                    SELECT 1
                    FROM [sales].[OrderLines] AS [o1] WITH (UPDLOCK, ROWLOCK)
                    WHERE [o1].[OrderId] = [o0].[Id] AND [o1].[Quantity] > 5))
            ORDER BY COALESCE([o3].[Quantity], 0) DESC, [o].[Id]
            OFFSET @p ROWS FETCH NEXT @p3 ROWS ONLY
            OPTION (MAXDOP 1, RECOMPILE)
            """,
            sql);
    }

    [Fact]
    public void HintsFromUnionBranchesApplyThroughoutJoinsGroupingAndPagination()
    {
        using var context = new QueryHintTestContext();

        var largeLines = context.Orders
            .WithTableHints(TableHint.UpdLock())
            .WithQueryHints(QueryHint.MaxDop(2))
            .Join(
                context.OrderLines.Where(line => line.Quantity >= 10),
                order => order.Id,
                line => line.OrderId,
                (order, line) => new { order.CustomerId, line.Product, line.Quantity });
        var relatedLines = context.Orders
            .Where(order => order.Amount < 100m)
            .Join(
                context.Orders.WithTableHints(TableHint.RowLock()).WithQueryHints(QueryHint.Recompile()),
                order => order.CustomerId,
                related => related.CustomerId,
                (order, related) => new { order.Id, order.CustomerId, RelatedOrderId = related.Id })
            .Where(orders => orders.Id != orders.RelatedOrderId)
            .Join(
                context.OrderLines,
                orders => orders.RelatedOrderId,
                line => line.OrderId,
                (orders, line) => new { orders.CustomerId, line.Product, line.Quantity });
        var sql = largeLines
            .Union(relatedLines)
            .GroupBy(line => new { line.CustomerId, line.Product })
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                group.Key.CustomerId,
                group.Key.Product,
                TotalQuantity = group.Sum(line => line.Quantity)
            })
            .OrderByDescending(total => total.TotalQuantity)
            .ThenBy(total => total.CustomerId)
            .ThenBy(total => total.Product)
            .Skip(5)
            .Take(10)
            .WithQueryHints(QueryHint.HashUnion(), QueryHint.HashGroup())
            .ToQueryString();

        AssertSql(
            """
            DECLARE @p int = 5;
            DECLARE @p1 int = 10;

            SELECT [u].[CustomerId], [u].[Product], COALESCE(SUM([u].[Quantity]), 0) AS [TotalQuantity]
            FROM (
                SELECT [o].[CustomerId], [o1].[Product], [o1].[Quantity]
                FROM [sales].[Orders] AS [o] WITH (ROWLOCK, UPDLOCK)
                INNER JOIN (
                    SELECT [o0].[OrderId], [o0].[Product], [o0].[Quantity]
                    FROM [sales].[OrderLines] AS [o0] WITH (ROWLOCK, UPDLOCK)
                    WHERE [o0].[Quantity] >= 10
                ) AS [o1] ON [o].[Id] = [o1].[OrderId]
                UNION
                SELECT [o2].[CustomerId], [o4].[Product], [o4].[Quantity]
                FROM [sales].[Orders] AS [o2] WITH (ROWLOCK, UPDLOCK)
                INNER JOIN [sales].[Orders] AS [o3] WITH (ROWLOCK, UPDLOCK) ON [o2].[CustomerId] = [o3].[CustomerId]
                INNER JOIN [sales].[OrderLines] AS [o4] WITH (ROWLOCK, UPDLOCK) ON [o3].[Id] = [o4].[OrderId]
                WHERE [o2].[Amount] < 100.0 AND [o2].[Id] <> [o3].[Id]
            ) AS [u]
            GROUP BY [u].[CustomerId], [u].[Product]
            HAVING COUNT(*) > 1
            ORDER BY COALESCE(SUM([u].[Quantity]), 0) DESC, [u].[CustomerId], [u].[Product]
            OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY
            OPTION (HASH UNION, HASH GROUP, MAXDOP 2, RECOMPILE)
            """,
            sql);
    }

    [Fact]
    public void HintsFromNestedSubqueriesApplyThroughoutPagedCorrelatedTopTwoQuery()
    {
        using var context = new QueryHintTestContext();

        var orders = context.Orders
            .OrderByDescending(order => order.Amount)
            .ThenBy(order => order.Id)
            .Skip(5)
            .Take(10);
        var query =
            from order in orders
            from line in context.OrderLines
                .WithQueryHints(QueryHint.MaxDop(1))
                .Where(line => line.OrderId == order.Id
                    && !context.OrderLines
                        .WithTableHints(TableHint.NoLock())
                        .Any(other => other.Product == line.Product
                            && other.OrderId != order.Id && other.Quantity > line.Quantity))
                .OrderByDescending(line => line.Quantity)
                .ThenBy(line => line.Id)
                .Take(2)
                .DefaultIfEmpty()
            select new
            {
                order.Id,
                LineId = (int?)line.Id,
                line.Product,
                TotalQuantity = context.OrderLines
                    .Where(other => other.OrderId == order.Id)
                    .Sum(other => (int?)other.Quantity) ?? 0
            };
        var sql = query
            .WithQueryHints(QueryHint.Recompile())
            .ToQueryString();

        AssertSql(
            """
            DECLARE @p int = 5;
            DECLARE @p1 int = 10;

            SELECT [o2].[Id], [o3].[Id] AS [LineId], [o3].[Product], COALESCE((
                SELECT COALESCE(SUM([o4].[Quantity]), 0)
                FROM [sales].[OrderLines] AS [o4] WITH (NOLOCK)
                WHERE [o4].[OrderId] = [o2].[Id]), 0) AS [TotalQuantity]
            FROM (
                SELECT [o].[Id], [o].[Amount]
                FROM [sales].[Orders] AS [o] WITH (NOLOCK)
                ORDER BY [o].[Amount] DESC, [o].[Id]
                OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY
            ) AS [o2]
            OUTER APPLY (
                SELECT TOP(2) [o0].[Id], [o0].[Product]
                FROM [sales].[OrderLines] AS [o0] WITH (NOLOCK)
                WHERE [o0].[OrderId] = [o2].[Id] AND NOT EXISTS (
                    SELECT 1
                    FROM [sales].[OrderLines] AS [o1] WITH (NOLOCK)
                    WHERE [o1].[Product] = [o0].[Product] AND [o1].[OrderId] <> [o2].[Id] AND [o1].[Quantity] > [o0].[Quantity])
                ORDER BY [o0].[Quantity] DESC, [o0].[Id]
            ) AS [o3]
            ORDER BY [o2].[Amount] DESC, [o2].[Id]
            OPTION (MAXDOP 1, RECOMPILE)
            """,
            sql);
    }

    [Fact]
    public void HintTagsAreRemovedFromTheCommentHeaderWhileOtherTagsRemain()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .TagWith("Orders page")
            .WithQueryHints(QueryHint.OptimizeForUnknown())
            .WithTableHints(TableHint.NoLock())
            .ToQueryString();

        AssertSql(
            """
            -- Orders page

            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o] WITH (NOLOCK)
            OPTION (OPTIMIZE FOR UNKNOWN)
            """,
            sql);
        Assert.DoesNotContain(QueryHintTags.QueryHintPrefix, sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.TableHintPrefix, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedCallsCombineHintsAndWriteEachHintOnce()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .WithQueryHints(QueryHint.Recompile())
            .WithQueryHints(QueryHint.Recompile(), QueryHint.UseHint("DISABLE_PARAMETER_SNIFFING"))
            .WithTableHints(TableHint.NoLock())
            .WithTableHints(TableHint.NoLock())
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o] WITH (NOLOCK)
            OPTION (RECOMPILE, USE HINT('DISABLE_PARAMETER_SNIFFING'))
            """,
            sql);
    }

    [Fact]
    public void TableHintsFollowTheAliasOfTemporalTables()
    {
        using var context = new QueryHintTestContext();

        var sql = context.AuditEntries
            .TemporalAll()
            .WithTableHints(TableHint.NoLock())
            .ToQueryString();

        AssertSql(
            """
            SELECT [a].[Id], [a].[Action], [a].[PeriodEnd], [a].[PeriodStart]
            FROM [sales].[AuditEntries] FOR SYSTEM_TIME ALL AS [a] WITH (NOLOCK)
            """,
            sql);
    }

    [Fact]
    public async Task ExecuteUpdateAndExecuteDeleteCarryQueryAndTableHints()
    {
        var capture = new SqlCaptureInterceptor(suppressExecution: true);
        await using var context = new QueryHintTestContext(interceptors: capture);
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;

        await context.Orders
            .Where(order => order.CustomerId == 7)
            .WithQueryHints(QueryHint.MaxDop(1))
            .WithTableHints(TableHint.RowLock())
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Amount, 0m), cancellationToken);
        await context.Orders
            .Where(order => order.CustomerId == 7)
            .WithQueryHints(QueryHint.MaxDop(1))
            .WithTableHints(TableHint.RowLock())
            .ExecuteDeleteAsync(cancellationToken);

        Assert.Collection(capture.Commands,
            update => AssertSql(
                """
                UPDATE [o]
                SET [o].[Amount] = @p
                FROM [sales].[Orders] AS [o] WITH (ROWLOCK)
                WHERE [o].[CustomerId] = 7
                OPTION (MAXDOP 1)
                """,
                update),
            delete => AssertSql(
                """
                DELETE FROM [o]
                FROM [sales].[Orders] AS [o] WITH (ROWLOCK)
                WHERE [o].[CustomerId] = 7
                OPTION (MAXDOP 1)
                """,
                delete));
    }

    [Theory]
    [InlineData(QueryHintTags.QueryHintPrefix + "RECOMPILE) DROP TABLE [sales].[Orders]; --")]
    [InlineData(QueryHintTags.TableHintPrefix + "NOLOCK) DROP TABLE [sales].[Orders]; --")]
    public void HintTagsThatTheExtensionsDidNotCreateAreRejected(string tag)
    {
        using var context = new QueryHintTestContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.Orders.TagWith(tag).ToQueryString());

        Assert.Contains("supported SQL Server", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UncomposedRawSqlQueriesRejectHints()
    {
        using var context = new QueryHintTestContext();

        Assert.Throws<InvalidOperationException>(() => context.Orders
            .FromSql($"SELECT * FROM [sales].[Orders]")
            .WithQueryHints(QueryHint.Recompile())
            .ToQueryString());
        Assert.Throws<InvalidOperationException>(() => context.Database
            .SqlQuery<int>($"SELECT 1 AS [Value]")
            .WithTableHints(TableHint.NoLock())
            .ToQueryString());
    }

    [Fact]
    public void ComposedRawSqlQueriesTakeQueryHintsButNotTableHintsAlone()
    {
        using var context = new QueryHintTestContext();
        var composed = context.Orders
            .FromSql($"SELECT * FROM [sales].[Orders]")
            .Where(order => order.CustomerId == 42);

        var sql = composed.WithQueryHints(QueryHint.Recompile()).ToQueryString();

        var normalized = Normalize(sql);
        Assert.EndsWith("WHERE [e].[CustomerId] = 42\nOPTION (RECOMPILE)", normalized, StringComparison.Ordinal);
        Assert.Equal(1, normalized.Split("OPTION (RECOMPILE)").Length - 1);
        var exception = Assert.Throws<InvalidOperationException>(() => composed.WithTableHints(TableHint.NoLock()).ToQueryString());
        Assert.Contains("would be read without the hint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TableHintsStillApplyWhenThePredicateUsesAParameterizedCollection()
    {
        using var context = new QueryHintTestContext();
        var ids = Enumerable.Range(1, 3).ToArray();

        var sql = context.Orders
            .Where(order => ids.Contains(order.Id))
            .WithTableHints(TableHint.NoLock())
            .ToQueryString();

        Assert.Contains("[o] WITH (NOLOCK)", sql, StringComparison.Ordinal);
        Assert.Contains("@ids", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TableHintsRejectRawSqlJoinedToATable()
    {
        using var context = new QueryHintTestContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.Orders
            .FromSqlRaw("SELECT * FROM [sales].[Orders]")
            .Join(
                context.OrderLines,
                order => order.Id,
                line => line.OrderId,
                (order, line) => order)
            .WithTableHints(TableHint.UpdLock())
            .ToQueryString());

        Assert.Contains("would be read without the hint", exception.Message, StringComparison.Ordinal);
        Assert.Contains("FromSql", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupAndPlanHintsAreWrittenTogether()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .WithQueryHints(
                QueryHint.HashGroup(),
                QueryHint.OrderGroup(),
                QueryHint.KeepPlan(),
                QueryHint.KeepFixedPlan())
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o]
            OPTION (HASH GROUP, ORDER GROUP, KEEP PLAN, KEEPFIXED PLAN)
            """,
            sql);
    }

    [Fact]
    public void LockingHintsThatSqlServerRejectsThrowBeforeACommandIsSent()
    {
        using var context = new QueryHintTestContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.Orders
            .WithTableHints(TableHint.NoLock())
            .WithTableHints(TableHint.UpdLock())
            .ToQueryString());

        Assert.Contains("NOLOCK", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HintsInsideQueryFiltersAreTranslated()
    {
        using var context = new HintFilterContext();

        var sql = context.Orders.Where(order => order.CustomerId == 42).ToQueryString();
        var ignored = context.Orders.IgnoreQueryFilters().Where(order => order.CustomerId == 42).ToQueryString();

        Assert.Contains("WITH (NOLOCK)", sql, StringComparison.Ordinal);
        Assert.Contains("OPTION (RECOMPILE)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.TableHintPrefix, sql, StringComparison.Ordinal);
        Assert.DoesNotContain("WITH (NOLOCK)", ignored, StringComparison.Ordinal);
        Assert.DoesNotContain("OPTION (RECOMPILE)", ignored, StringComparison.Ordinal);
    }

    [Fact]
    public void HintArgumentsCanUseLambdasThatAreNotQueryParameters()
    {
        using var context = new QueryHintTestContext();
        var list = new QueryHint?[] { QueryHint.Recompile(), null };

        var sql = context.Orders
            .Where(order => context.OrderLines
                .Where(line => line.OrderId == order.Id)
                .WithQueryHints(list.Where(hint => hint != null).ToArray()!)
                .Any())
            .ToQueryString();

        Assert.Contains("OPTION (RECOMPILE)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.QueryHintPrefix, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void HintsThrowWhenTheExtensionsAreNotRegistered()
    {
        var options = new DbContextOptionsBuilder<QueryHintTestContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NotUsed")
            .Options;
        using var context = new QueryHintTestContext(options);

        var exception = Assert.Throws<InvalidOperationException>(
            () => context.Orders.WithTableHints(TableHint.UpdLock()));

        Assert.Contains("UseEntityFrameworkCoreExtensions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SplitQueryPrimaryCommandKeepsHints()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .Include(order => order.Lines)
            .AsSplitQuery()
            .Where(order => order.CustomerId == 42)
            .WithQueryHints(QueryHint.Recompile())
            .WithTableHints(TableHint.NoLock())
            .ToQueryString();

        Assert.Contains("WITH (NOLOCK)", sql, StringComparison.Ordinal);
        Assert.Contains("OPTION (RECOMPILE)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.QueryHintPrefix, sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.TableHintPrefix, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinAndUnionStrategyHintsAreWrittenTogether()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .Join(context.OrderLines, order => order.Id, line => line.OrderId, (order, line) => order)
            .WithQueryHints(
                QueryHint.HashJoin(),
                QueryHint.MergeJoin(),
                QueryHint.ConcatUnion(),
                QueryHint.HashUnion())
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o]
            INNER JOIN [sales].[OrderLines] AS [o0] ON [o].[Id] = [o0].[OrderId]
            OPTION (HASH JOIN, MERGE JOIN, CONCAT UNION, HASH UNION)
            """,
            sql);
    }

    [Fact]
    public void HintsCombinedAcrossCallsRejectConflictingSingletonValues()
    {
        using var context = new QueryHintTestContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.Orders
            .WithQueryHints(QueryHint.MaxDop(1))
            .WithQueryHints(QueryHint.MaxDop(2))
            .ToQueryString());

        Assert.Contains("only one MAXDOP hint", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MAXDOP 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MAXDOP 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompiledQueriesTranslateHintsAfterParameterFilters()
    {
        var compiled = EF.CompileQuery((QueryHintTestContext context, int id) => context.Orders
            .Where(order => order.Id == id)
            .WithQueryHints(QueryHint.Recompile())
            .WithTableHints(TableHint.NoLock()));

        using var context = new QueryHintTestContext();
        var sql = ((IQueryingEnumerable)compiled(context, 42)).ToQueryString();

        Assert.Contains("WITH (NOLOCK)", sql, StringComparison.Ordinal);
        Assert.Contains("OPTION (RECOMPILE)", sql, StringComparison.Ordinal);
        Assert.Contains("= 42", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.QueryHintPrefix, sql, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.TableHintPrefix, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CapturedHintsInsideCorrelatedSubqueriesAreTranslated()
    {
        using var context = new QueryHintTestContext();
        var hint = TableHint.NoLock();
        var hints = new[] { TableHint.NoLock() };
        var queryHint = QueryHint.Recompile();
        var degreeOfParallelism = 2;

        var single = context.Orders
            .Where(order => context.OrderLines
                .Where(line => line.OrderId == order.Id)
                .WithTableHints(hint)
                .Any())
            .ToQueryString();
        var capturedArray = context.Orders
            .Where(order => context.OrderLines
                .Where(line => line.OrderId == order.Id)
                .WithTableHints(hints)
                .WithQueryHints(queryHint)
                .Any())
            .ToQueryString();
        var capturedArgument = context.Orders
            .Where(order => context.OrderLines
                .Where(line => line.OrderId == order.Id)
                .WithQueryHints(QueryHint.MaxDop(degreeOfParallelism))
                .Any())
            .ToQueryString();

        Assert.Contains("WITH (NOLOCK)", single, StringComparison.Ordinal);
        Assert.Contains("WITH (NOLOCK)", capturedArray, StringComparison.Ordinal);
        Assert.Contains("OPTION (RECOMPILE)", capturedArray, StringComparison.Ordinal);
        Assert.Contains("OPTION (MAXDOP 2)", capturedArgument, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.QueryHintPrefix, capturedArray, StringComparison.Ordinal);
        Assert.DoesNotContain(QueryHintTags.TableHintPrefix, capturedArray, StringComparison.Ordinal);
    }

    [Fact]
    public void HintsInsideCorrelatedSubqueriesAreTranslated()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders
            .Where(order => context.OrderLines
                .Where(line => line.OrderId == order.Id)
                .WithQueryHints(QueryHint.Recompile())
                .WithTableHints(TableHint.NoLock())
                .Any())
            .ToQueryString();

        AssertSql(
            """
            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o] WITH (NOLOCK)
            WHERE EXISTS (
                SELECT 1
                FROM [sales].[OrderLines] AS [o0] WITH (NOLOCK)
                WHERE [o0].[OrderId] = [o].[Id])
            OPTION (RECOMPILE)
            """,
            sql);
    }

    [Fact]
    public void TableHintsAreOmittedWhenOptimizationRemovesEveryTable()
    {
        var capture = new SqlCaptureInterceptor(suppressExecution: true);
        using var context = new QueryHintTestContext(interceptors: capture);
        var ids = Array.Empty<int>();

        var anyException = Record.Exception(() => context.Orders
            .Where(order => ids.Contains(order.Id))
            .WithQueryHints(QueryHint.Recompile())
            .WithTableHints(TableHint.NoLock())
            .Any());
        var allException = Record.Exception(() => context.Orders
            .WithTableHints(TableHint.NoLock())
            .All(order => true));

        Assert.DoesNotContain("would be read without the hint", anyException?.ToString() ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain("would be read without the hint", allException?.ToString() ?? "", StringComparison.Ordinal);
        Assert.NotEmpty(capture.Commands);
        Assert.All(capture.Commands, command => Assert.DoesNotContain("WITH (", command, StringComparison.Ordinal));
        Assert.Contains("OPTION (RECOMPILE)", capture.Commands[0], StringComparison.Ordinal);
    }

    [Fact]
    public void QueriesWithoutHintsAreUnchanged()
    {
        using var context = new QueryHintTestContext();

        var sql = context.Orders.TagWith("Plain").Where(order => order.CustomerId == 1).ToQueryString();

        Assert.Equal(
            """
            -- Plain

            SELECT [o].[Id], [o].[Amount], [o].[CustomerId]
            FROM [sales].[Orders] AS [o]
            WHERE [o].[CustomerId] = 1
            """,
                sql);
    }

    [Fact]
    public async Task NonRelationalProvidersIgnoreHints()
    {
        var options = new DbContextOptionsBuilder<QueryHintTestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        await using var context = new QueryHintTestContext(options);
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        context.Orders.Add(new HintOrder { CustomerId = 3, Amount = 5m });
        await context.SaveChangesAsync(cancellationToken);

        var orders = await context.Orders
            .WithQueryHints(QueryHint.Recompile())
            .WithTableHints(TableHint.NoLock())
            .ToListAsync(cancellationToken);

        Assert.Equal(3, Assert.Single(orders).CustomerId);

        var compiled = EF.CompileQuery((QueryHintTestContext compiledContext, int customerId) => compiledContext.Orders
            .Where(order => order.CustomerId == customerId)
            .WithQueryHints(QueryHint.Recompile())
            .WithTableHints(TableHint.NoLock()));
        Assert.Equal(3, Assert.Single(compiled(context, 3)).CustomerId);

        var hint = TableHint.NoLock();
        var degreeOfParallelism = 1;
        var compiledCaptured = EF.CompileQuery((QueryHintTestContext compiledContext, int customerId) => compiledContext.Orders
            .Where(order => order.CustomerId == customerId)
            .WithQueryHints(QueryHint.MaxDop(degreeOfParallelism))
            .WithTableHints(hint));
        Assert.Equal(3, Assert.Single(compiledCaptured(context, 3)).CustomerId);
        Assert.False(context.Orders.Any(order => context.OrderLines
            .Where(line => line.OrderId == order.Id)
            .WithTableHints(hint)
            .Any()));
    }

    private sealed class HintFilterContext : DbContext
    {
        public HintFilterContext()
            : base(new DbContextOptionsBuilder<HintFilterContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NotUsed")
                .UseEntityFrameworkCoreExtensions()
                .Options)
        {
        }

        public DbSet<HintOrder> Orders => Set<HintOrder>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HintOrder>().ToTable("Orders", "sales");
            modelBuilder.Entity<HintOrder>().Property(entity => entity.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<HintOrderLine>().ToTable("OrderLines", "sales");
            modelBuilder.Entity<HintOrderLine>().Property(entity => entity.Product).HasMaxLength(100);
            modelBuilder.Entity<HintOrder>().HasQueryFilter(order => Set<HintOrderLine>()
                .WithQueryHints(QueryHint.Recompile())
                .WithTableHints(TableHint.NoLock())
                .Any(line => line.OrderId == order.Id));
        }
    }

    private static void AssertSql(string expected, string actual)
        => Assert.Equal(Normalize(expected), Normalize(actual));

    private static string Normalize(string sql) => sql.ReplaceLineEndings("\n").Trim();
}
