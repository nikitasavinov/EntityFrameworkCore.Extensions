using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Adds SQL Server query and table hints to LINQ queries.
/// </summary>
/// <remarks>
/// Hints are applied only when the context is configured with
/// <see cref="DbContextOptionsBuilderExtensions.UseEntityFrameworkCoreExtensions(DbContextOptionsBuilder)" />.
/// An EF Core query throws if that call is missing, because the hint would otherwise be ignored.
/// They cover every command of a split query, compiled queries, correlated subqueries, global query filters, and
/// the statements that <c>ExecuteUpdate</c> and <c>ExecuteDelete</c> run. Non-relational providers such as
/// InMemory ignore them. Hints in one call are emitted in the order given. SQL Server rejects some combinations;
/// those are rejected here before a command is sent.
/// </remarks>
public static class QueryableExtensions
{
    /// <summary>
    /// Adds SQL Server query hints to the <c>OPTION</c> clause of the generated statement.
    /// </summary>
    /// <typeparam name="T">The type of the query elements.</typeparam>
    /// <param name="source">The query to add hints to.</param>
    /// <param name="hints">The hints to add. Hints from repeated calls are combined.</param>
    /// <returns>The query with the hints applied.</returns>
    /// <exception cref="ArgumentException"><paramref name="hints" /> is empty or contains <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The hints are a combination SQL Server does not allow, or the context was not configured with <see cref="DbContextOptionsBuilderExtensions.UseEntityFrameworkCoreExtensions(DbContextOptionsBuilder)" />.</exception>
    /// <seealso cref="QueryHint" />
    public static IQueryable<T> WithQueryHints<T>(this IQueryable<T> source, params QueryHint[] hints)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tag = CreateQueryHintTag(hints);
        HintConfiguration.EnsureConfigured(source);
        return source.TagWith(tag);
    }

    /// <summary>
    /// Adds SQL Server table hints as a <c>WITH (...)</c> clause to every table that the generated statement references,
    /// including joined tables and tables in subqueries.
    /// </summary>
    /// <remarks>
    /// Locking hints such as <see cref="TableHint.UpdLock" /> hold their locks only until the current transaction
    /// completes, so start a transaction before running the query.
    /// </remarks>
    /// <typeparam name="T">The type of the query elements.</typeparam>
    /// <param name="source">The query to add hints to.</param>
    /// <param name="hints">The hints to add. Hints from repeated calls are combined.</param>
    /// <returns>The query with the hints applied.</returns>
    /// <exception cref="ArgumentException"><paramref name="hints" /> is empty or contains <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The hints are a combination SQL Server does not allow, or the context was not configured with <see cref="DbContextOptionsBuilderExtensions.UseEntityFrameworkCoreExtensions(DbContextOptionsBuilder)" />.</exception>
    /// <seealso cref="TableHint" />
    public static IQueryable<T> WithTableHints<T>(this IQueryable<T> source, params TableHint[] hints)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tag = CreateTableHintTag(hints);
        HintConfiguration.EnsureConfigured(source);
        return source.TagWith(tag);
    }

    internal static string CreateQueryHintTag(QueryHint[] hints)
    {
        ValidateHints(hints);
        var sql = HintCompatibility.Dedupe(hints.Select(static hint => hint.Sql).ToArray());
        HintCompatibility.EnsureQueryHints(sql);
        return QueryHintTags.CreateQueryHints(sql);
    }

    internal static string CreateTableHintTag(TableHint[] hints)
    {
        ValidateHints(hints);
        var sql = HintCompatibility.Dedupe(hints.Select(static hint => hint.Sql).ToArray());
        HintCompatibility.EnsureTableHints(sql);
        return QueryHintTags.CreateTableHints(sql);
    }

    private static void ValidateHints<THint>(THint[] hints)
        where THint : class
    {
        ArgumentNullException.ThrowIfNull(hints);
        if (hints.Length == 0)
        {
            throw new ArgumentException("At least one hint must be specified.", nameof(hints));
        }

        if (Array.IndexOf(hints, null) >= 0)
        {
            throw new ArgumentException("Hints cannot contain null.", nameof(hints));
        }
    }
}
