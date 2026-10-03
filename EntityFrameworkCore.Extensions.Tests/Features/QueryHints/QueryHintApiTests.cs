using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class QueryHintApiTests
{
    [Fact]
    public void QueryHintFactoriesProduceSqlServerHintText()
        => Assert.All(QueryHintCatalog.QueryHints, item => Assert.Equal(item.Sql, item.Hint.ToString()));

    [Fact]
    public void TableHintFactoriesProduceSqlServerHintText()
        => Assert.All(QueryHintCatalog.TableHints, item => Assert.Equal(item.Sql, item.Hint.ToString()));

    [Fact]
    public void EveryFactoryHintRoundTripsThroughTagValidation()
    {
        Assert.All(QueryHintCatalog.QueryHints, item =>
        {
            Assert.True(QueryHint.TryParse(item.Sql, out var parsed));
            Assert.Equal(item.Sql, parsed.ToString());
        });
        Assert.All(QueryHintCatalog.TableHints, item =>
        {
            Assert.True(TableHint.TryParse(item.Sql, out var parsed));
            Assert.Equal(item.Sql, parsed.ToString());
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("recompile")]
    [InlineData("RECOMPILE) DROP TABLE [Orders]; --")]
    [InlineData("MAXDOP -1")]
    [InlineData("MAXDOP 04")]
    [InlineData("MAXDOP 32768")]
    [InlineData("MAXDOP 99999999999")]
    [InlineData("MAX_GRANT_PERCENT = 100.5")]
    [InlineData("MAX_GRANT_PERCENT = 12.50")]
    [InlineData("FAST 0")]
    [InlineData("USE HINT('A'), MAXDOP 1")]
    [InlineData("USE HINT('A''); --')")]
    [InlineData("OPTIMIZE FOR (@p = 1)")]
    public void TagValidationRejectsTextThatNoQueryHintFactoryProduces(string sql)
        => Assert.False(QueryHint.TryParse(sql, out _));

    [Theory]
    [InlineData("")]
    [InlineData("nolock")]
    [InlineData("NOLOCK, UPDLOCK")]
    [InlineData("NOLOCK) DROP TABLE [Orders]; --")]
    [InlineData("INDEX(1)")]
    [InlineData("SNAPSHOT")]
    public void TagValidationRejectsTextThatNoTableHintFactoryProduces(string sql)
        => Assert.False(TableHint.TryParse(sql, out _));

    [Theory]
    [InlineData(-1)]
    [InlineData(32_768)]
    public void MaxDopAndMaxRecursionRejectOutOfRangeValues(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QueryHint.MaxDop(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => QueryHint.MaxRecursion(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void FastRejectsNonPositiveRows(int rows)
        => Assert.Throws<ArgumentOutOfRangeException>(() => QueryHint.Fast(rows));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void GrantPercentHintsRejectValuesOutsideZeroToHundred(double percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QueryHint.MaxGrantPercent(percent));
        Assert.Throws<ArgumentOutOfRangeException>(() => QueryHint.MinGrantPercent(percent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("DISABLE PARAMETER SNIFFING")]
    [InlineData("DISABLE_PARAMETER_SNIFFING')")]
    [InlineData("HINT;")]
    public void UseHintRejectsNamesWithUnsupportedCharacters(string hintName)
        => Assert.Throws<ArgumentException>(() => QueryHint.UseHint(hintName));

    [Fact]
    public void UseHintRejectsNull()
        => Assert.Throws<ArgumentNullException>(() => QueryHint.UseHint(null!));

    [Fact]
    public void WithHintMethodsValidateArguments()
    {
        var source = Array.Empty<int>().AsQueryable();

        Assert.Throws<ArgumentNullException>(() => ((IQueryable<int>)null!).WithQueryHints(QueryHint.Recompile()));
        Assert.Throws<ArgumentNullException>(() => source.WithQueryHints(null!));
        Assert.Throws<ArgumentException>(() => source.WithQueryHints());
        Assert.Throws<ArgumentException>(() => source.WithQueryHints(QueryHint.Recompile(), null!));

        Assert.Throws<ArgumentNullException>(() => ((IQueryable<int>)null!).WithTableHints(TableHint.NoLock()));
        Assert.Throws<ArgumentNullException>(() => source.WithTableHints(null!));
        Assert.Throws<ArgumentException>(() => source.WithTableHints());
        Assert.Throws<ArgumentException>(() => source.WithTableHints(TableHint.NoLock(), null!));
    }

    [Fact]
    public void ConflictingHintsAreRejectedBeforeSqlIsGenerated()
    {
        var source = Array.Empty<int>().AsQueryable();

        Assert.Same(source, source.WithQueryHints(QueryHint.HashJoin(), QueryHint.MergeJoin()));
        Assert.Same(source, source.WithQueryHints(QueryHint.ConcatUnion(), QueryHint.HashUnion(), QueryHint.MergeUnion()));
        Assert.Same(source, source.WithQueryHints(QueryHint.HashGroup(), QueryHint.OrderGroup()));
        Assert.Same(source, source.WithQueryHints(QueryHint.KeepPlan(), QueryHint.KeepFixedPlan()));
        Assert.Same(source, source.WithQueryHints(QueryHint.MaxDop(1), QueryHint.MaxDop(1)));
        Assert.Same(source, source.WithTableHints(TableHint.UpdLock(), TableHint.ReadPast(), TableHint.RowLock()));
        Assert.Same(source, source.WithTableHints(TableHint.UpdLock(), TableHint.HoldLock()));
        Assert.Same(source, source.WithTableHints(TableHint.XLock(), TableHint.TabLock()));

        AssertConflictingSingleton(
            () => source.WithQueryHints(QueryHint.MaxDop(1), QueryHint.MaxDop(2)),
            "MAXDOP",
            "MAXDOP 1",
            "MAXDOP 2");
        AssertConflictingSingleton(
            () => source.WithQueryHints(QueryHint.MaxRecursion(0), QueryHint.MaxRecursion(100)),
            "MAXRECURSION",
            "MAXRECURSION 0",
            "MAXRECURSION 100");
        AssertConflictingSingleton(
            () => source.WithQueryHints(QueryHint.Fast(1), QueryHint.Fast(10)),
            "FAST",
            "FAST 1",
            "FAST 10");
        AssertConflictingSingleton(
            () => source.WithQueryHints(QueryHint.MaxGrantPercent(10), QueryHint.MaxGrantPercent(20)),
            "MAX_GRANT_PERCENT",
            "MAX_GRANT_PERCENT = 10",
            "MAX_GRANT_PERCENT = 20");
        AssertConflictingSingleton(
            () => source.WithQueryHints(QueryHint.MinGrantPercent(10), QueryHint.MinGrantPercent(20)),
            "MIN_GRANT_PERCENT",
            "MIN_GRANT_PERCENT = 10",
            "MIN_GRANT_PERCENT = 20");

        Assert.Throws<InvalidOperationException>(
            () => source.WithQueryHints(QueryHint.MinGrantPercent(80), QueryHint.MaxGrantPercent(10)));

        var granularity = Assert.Throws<InvalidOperationException>(
            () => source.WithTableHints(TableHint.NoLock(), TableHint.RowLock()));
        Assert.Contains("only one granularity table hint", granularity.Message, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(
            () => source.WithTableHints(TableHint.NoLock(), TableHint.HoldLock()));
        Assert.Throws<InvalidOperationException>(
            () => source.WithTableHints(TableHint.ForceSeek(), TableHint.ForceScan()));
        AssertIncompatible(
            () => source.WithTableHints(TableHint.NoLock(), TableHint.UpdLock()),
            "NOLOCK",
            "UPDLOCK");
        AssertIncompatible(
            () => source.WithTableHints(TableHint.NoLock(), TableHint.XLock()),
            "NOLOCK",
            "XLOCK");
        AssertIncompatible(
            () => source.WithTableHints(TableHint.ReadUncommitted(), TableHint.UpdLock()),
            "READUNCOMMITTED",
            "UPDLOCK");
        AssertIncompatible(
            () => source.WithTableHints(TableHint.ReadPast(), TableHint.NoLock()),
            "READPAST",
            "NOLOCK");
        AssertIncompatible(
            () => source.WithTableHints(TableHint.ReadPast(), TableHint.ReadUncommitted()),
            "READPAST",
            "READUNCOMMITTED");
        AssertIncompatible(
            () => source.WithTableHints(TableHint.ReadPast(), TableHint.HoldLock()),
            "READPAST",
            "HOLDLOCK");
        AssertIncompatible(
            () => source.WithTableHints(TableHint.Serializable(), TableHint.ReadPast()),
            "READPAST",
            "SERIALIZABLE");
        Assert.Same(source, source.WithTableHints(TableHint.ReadPast(), TableHint.RepeatableRead()));

        var locking = Assert.Throws<InvalidOperationException>(
            () => source.WithTableHints(TableHint.UpdLock(), TableHint.XLock()));
        Assert.Contains("only one locking table hint", locking.Message, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK", locking.Message, StringComparison.Ordinal);
        Assert.Contains("XLOCK", locking.Message, StringComparison.Ordinal);
    }

    private static void AssertIncompatible(Action act, string first, string second)
    {
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("does not allow", exception.Message, StringComparison.Ordinal);
        Assert.Contains(first, exception.Message, StringComparison.Ordinal);
        Assert.Contains(second, exception.Message, StringComparison.Ordinal);
    }

    private static void AssertConflictingSingleton(Action act, string kind, string first, string second)
    {
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains($"only one {kind} hint", exception.Message, StringComparison.Ordinal);
        Assert.Contains(first, exception.Message, StringComparison.Ordinal);
        Assert.Contains(second, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithHintMethodsLeaveNonEntityFrameworkQueriesUnchanged()
    {
        var source = Enumerable.Range(1, 3).AsQueryable();

        Assert.Same(source, source.WithQueryHints(QueryHint.Recompile()));
        Assert.Same(source, source.WithTableHints(TableHint.NoLock()));
    }
}
