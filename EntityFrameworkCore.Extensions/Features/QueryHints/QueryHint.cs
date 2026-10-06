using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Represents a SQL Server query hint that
/// <see cref="QueryableExtensions.WithQueryHints{T}(IQueryable{T}, QueryHint[])" /> adds to a statement's
/// <c>OPTION</c> clause.
/// </summary>
/// <seealso href="https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-query" />
public sealed partial class QueryHint
{
    private const int MaximumDegreeOfParallelism = 32_767;
    private const int MaximumRecursion = 32_767;

    private static readonly HashSet<string> FixedHints =
    [
        "RECOMPILE",
        "OPTIMIZE FOR UNKNOWN",
        "FORCE ORDER",
        "HASH JOIN",
        "LOOP JOIN",
        "MERGE JOIN",
        "HASH GROUP",
        "ORDER GROUP",
        "CONCAT UNION",
        "HASH UNION",
        "MERGE UNION",
        "KEEP PLAN",
        "KEEPFIXED PLAN",
        "ROBUST PLAN",
        "EXPAND VIEWS",
        "NO_PERFORMANCE_SPOOL",
        "IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX"
    ];

    private QueryHint(string sql)
    {
        Sql = sql;
    }

    internal string Sql { get; }

    /// <summary>Creates the <c>RECOMPILE</c> hint, which compiles a new plan for every execution.</summary>
    /// <returns><c>RECOMPILE</c>.</returns>
    public static QueryHint Recompile() => new("RECOMPILE");

    /// <summary>Creates the <c>MAXDOP</c> hint, which limits the degree of parallelism.</summary>
    /// <param name="degreeOfParallelism">The maximum degree of parallelism, from 0 to 32767. 0 lets SQL Server choose.</param>
    /// <returns><c>MAXDOP</c> with the specified value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="degreeOfParallelism" /> is outside the supported range.</exception>
    public static QueryHint MaxDop(int degreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(degreeOfParallelism);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(degreeOfParallelism, MaximumDegreeOfParallelism);

        return new($"MAXDOP {degreeOfParallelism.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>Creates the <c>MAXRECURSION</c> hint, which limits the recursion depth of recursive queries.</summary>
    /// <param name="number">The maximum number of recursions, from 0 to 32767. 0 removes the limit.</param>
    /// <returns><c>MAXRECURSION</c> with the specified value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number" /> is outside the supported range.</exception>
    public static QueryHint MaxRecursion(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaximumRecursion);

        return new($"MAXRECURSION {number.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>Creates the <c>OPTIMIZE FOR UNKNOWN</c> hint, which compiles the plan without sniffing parameter values.</summary>
    /// <returns><c>OPTIMIZE FOR UNKNOWN</c>.</returns>
    public static QueryHint OptimizeForUnknown() => new("OPTIMIZE FOR UNKNOWN");

    /// <summary>Creates the <c>FORCE ORDER</c> hint, which preserves the join order of the generated SQL.</summary>
    /// <returns><c>FORCE ORDER</c>.</returns>
    public static QueryHint ForceOrder() => new("FORCE ORDER");

    /// <summary>Creates the <c>HASH JOIN</c> hint, which restricts joins to hash joins.</summary>
    /// <returns><c>HASH JOIN</c>.</returns>
    public static QueryHint HashJoin() => new("HASH JOIN");

    /// <summary>Creates the <c>LOOP JOIN</c> hint, which restricts joins to nested loop joins.</summary>
    /// <returns><c>LOOP JOIN</c>.</returns>
    public static QueryHint LoopJoin() => new("LOOP JOIN");

    /// <summary>Creates the <c>MERGE JOIN</c> hint, which restricts joins to merge joins.</summary>
    /// <returns><c>MERGE JOIN</c>.</returns>
    public static QueryHint MergeJoin() => new("MERGE JOIN");

    /// <summary>Creates the <c>HASH GROUP</c> hint, which uses hashing for aggregations.</summary>
    /// <returns><c>HASH GROUP</c>.</returns>
    public static QueryHint HashGroup() => new("HASH GROUP");

    /// <summary>Creates the <c>ORDER GROUP</c> hint, which uses ordering for aggregations.</summary>
    /// <returns><c>ORDER GROUP</c>.</returns>
    public static QueryHint OrderGroup() => new("ORDER GROUP");

    /// <summary>Creates the <c>CONCAT UNION</c> hint, which uses concatenation for <c>UNION</c> operations.</summary>
    /// <returns><c>CONCAT UNION</c>.</returns>
    public static QueryHint ConcatUnion() => new("CONCAT UNION");

    /// <summary>Creates the <c>HASH UNION</c> hint, which uses hashing for <c>UNION</c> operations.</summary>
    /// <returns><c>HASH UNION</c>.</returns>
    public static QueryHint HashUnion() => new("HASH UNION");

    /// <summary>Creates the <c>MERGE UNION</c> hint, which uses merging for <c>UNION</c> operations.</summary>
    /// <returns><c>MERGE UNION</c>.</returns>
    public static QueryHint MergeUnion() => new("MERGE UNION");

    /// <summary>Creates the <c>FAST</c> hint, which optimizes the plan for retrieving the first rows quickly.</summary>
    /// <param name="rows">The number of rows to optimize for. Must be greater than zero.</param>
    /// <returns><c>FAST</c> with the specified value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows" /> is less than 1.</exception>
    public static QueryHint Fast(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);

        return new($"FAST {rows.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>Creates the <c>KEEP PLAN</c> hint, which relaxes the recompilation threshold.</summary>
    /// <returns><c>KEEP PLAN</c>.</returns>
    public static QueryHint KeepPlan() => new("KEEP PLAN");

    /// <summary>Creates the <c>KEEPFIXED PLAN</c> hint, which prevents recompilation caused by statistics changes.</summary>
    /// <returns><c>KEEPFIXED PLAN</c>.</returns>
    public static QueryHint KeepFixedPlan() => new("KEEPFIXED PLAN");

    /// <summary>Creates the <c>ROBUST PLAN</c> hint, which favors plans that support the maximum potential row size.</summary>
    /// <returns><c>ROBUST PLAN</c>.</returns>
    public static QueryHint RobustPlan() => new("ROBUST PLAN");

    /// <summary>Creates the <c>EXPAND VIEWS</c> hint, which expands indexed views instead of matching them.</summary>
    /// <returns><c>EXPAND VIEWS</c>.</returns>
    public static QueryHint ExpandViews() => new("EXPAND VIEWS");

    /// <summary>Creates the <c>NO_PERFORMANCE_SPOOL</c> hint, which prevents spool operators from being added to the plan.</summary>
    /// <returns><c>NO_PERFORMANCE_SPOOL</c>.</returns>
    public static QueryHint NoPerformanceSpool() => new("NO_PERFORMANCE_SPOOL");

    /// <summary>Creates the <c>IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX</c> hint, which keeps nonclustered columnstore indexes out of the plan.</summary>
    /// <returns><c>IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX</c>.</returns>
    public static QueryHint IgnoreNonclusteredColumnstoreIndex() => new("IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX");

    /// <summary>Creates the <c>MAX_GRANT_PERCENT</c> hint, which caps the query's memory grant.</summary>
    /// <param name="percent">The maximum memory grant as a percentage of the configured limit, from 0 to 100.</param>
    /// <returns><c>MAX_GRANT_PERCENT</c> with the specified value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent" /> is outside the supported range.</exception>
    public static QueryHint MaxGrantPercent(double percent) => new($"MAX_GRANT_PERCENT = {FormatPercent(percent)}");

    /// <summary>Creates the <c>MIN_GRANT_PERCENT</c> hint, which sets the query's minimum memory grant.</summary>
    /// <param name="percent">The minimum memory grant as a percentage of the configured limit, from 0 to 100.</param>
    /// <returns><c>MIN_GRANT_PERCENT</c> with the specified value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent" /> is outside the supported range.</exception>
    public static QueryHint MinGrantPercent(double percent) => new($"MIN_GRANT_PERCENT = {FormatPercent(percent)}");

    /// <summary>Creates a <c>USE HINT</c> hint, such as <c>USE HINT('DISABLE_PARAMETER_SNIFFING')</c>.</summary>
    /// <param name="hintName">The hint name, containing only letters, digits, and underscores.</param>
    /// <returns><c>USE HINT</c> with the specified hint name.</returns>
    /// <exception cref="ArgumentException"><paramref name="hintName" /> is empty or contains unsupported characters.</exception>
    /// <seealso href="https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-query#use_hint" />
    public static QueryHint UseHint(string hintName)
    {
        ArgumentNullException.ThrowIfNull(hintName);
        if (!HintNamePattern().IsMatch(hintName))
        {
            throw new ArgumentException(
                "USE HINT names can contain only letters, digits, and underscores.",
                nameof(hintName));
        }

        return new($"USE HINT('{hintName}')");
    }

    /// <summary>Returns the SQL text of the hint.</summary>
    /// <returns>The SQL text of the hint.</returns>
    public override string ToString() => Sql;

    /// <summary>
    /// Recreates a hint from its SQL text. Only text produced by this class's factory methods is accepted, so query
    /// tags that do not come from <see cref="QueryableExtensions" /> cannot inject SQL.
    /// </summary>
    internal static bool TryParse(string sql, [NotNullWhen(true)] out QueryHint? hint)
    {
        hint = FixedHints.Contains(sql) ? new QueryHint(sql) : ParseParameterizedHint(sql);
        if (hint is not null && string.Equals(hint.Sql, sql, StringComparison.Ordinal))
        {
            return true;
        }

        hint = null;
        return false;
    }

    private static QueryHint? ParseParameterizedHint(string sql)
    {
        try
        {
            if (IntegerHintPattern().Match(sql) is { Success: true } integerHint)
            {
                var value = int.Parse(integerHint.Groups["value"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
                return integerHint.Groups["name"].Value switch
                {
                    "MAXDOP" => MaxDop(value),
                    "MAXRECURSION" => MaxRecursion(value),
                    _ => Fast(value)
                };
            }

            if (PercentHintPattern().Match(sql) is { Success: true } percentHint)
            {
                var value = double.Parse(percentHint.Groups["value"].ValueSpan, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                return percentHint.Groups["name"].Value == "MAX_GRANT_PERCENT"
                    ? MaxGrantPercent(value)
                    : MinGrantPercent(value);
            }

            return UseHintPattern().Match(sql) is { Success: true } useHint
                ? UseHint(useHint.Groups["value"].Value)
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static string FormatPercent(double percent)
    {
        if (!(percent >= 0 && percent <= 100))
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "The percentage must be from 0 to 100.");
        }

        return percent.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex HintNamePattern();

    [GeneratedRegex("^(?<name>MAXDOP|MAXRECURSION|FAST) (?<value>[0-9]{1,10})$", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerHintPattern();

    [GeneratedRegex(@"^(?<name>MAX_GRANT_PERCENT|MIN_GRANT_PERCENT) = (?<value>[0-9]{1,3}(\.[0-9]{1,15})?)$", RegexOptions.CultureInvariant)]
    private static partial Regex PercentHintPattern();

    [GeneratedRegex(@"^USE HINT\('(?<value>[A-Za-z0-9_]{1,128})'\)$", RegexOptions.CultureInvariant)]
    private static partial Regex UseHintPattern();
}
