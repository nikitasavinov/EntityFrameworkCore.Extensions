using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's query SQL generator requires its provider-internal implementation.

internal sealed partial class ExtendedSqlServerQuerySqlGenerator
{
    private string? _tableHints;
    private bool _tableHintSourceRequiresTable;

    /// <inheritdoc />
    protected override void GenerateRootCommand(Expression queryExpression)
    {
        var tags = queryExpression switch
        {
            SelectExpression selectExpression => selectExpression.Tags,
            UpdateExpression updateExpression => MergeTags(updateExpression.Tags, updateExpression.SelectExpression.Tags),
            DeleteExpression deleteExpression => MergeTags(deleteExpression.Tags, deleteExpression.SelectExpression.Tags),
            _ => null
        };
        var (queryHints, tableHints) = tags is null ? ([], []) : QueryHintTags.Parse(tags);
        if (queryHints.Count == 0 && tableHints.Count == 0)
        {
            base.GenerateRootCommand(queryExpression);
            return;
        }

        // An uncomposed FromSql or SqlQuery statement is written verbatim, so the provider cannot tell where an
        // OPTION clause may go and has no table reference to attach table hints to.
        if (queryExpression is SelectExpression select && select.IsNonComposedFromSql())
        {
            throw new InvalidOperationException(
                "Query and table hints cannot be applied to a FromSql or SqlQuery query that is not composed with LINQ " +
                "operators. Write the hints in the SQL instead.");
        }

        _tableHints = tableHints.Count == 0 ? null : string.Join(", ", tableHints);
        _tableHintSourceRequiresTable = false;
        try
        {
            base.GenerateRootCommand(queryExpression);

            // A false predicate or a trivially true All() can remove every table during optimization, leaving
            // SELECT CAST(0 AS bit). That query has no table and no raw-SQL source, so the table hint is omitted.
            // FromSql, SqlQuery, and table-valued functions still have no place to put WITH. Fail even when some
            // other table in the same statement was hinted, because the raw rows would otherwise be read without it.
            if (_tableHints is not null && _tableHintSourceRequiresTable)
            {
                throw new InvalidOperationException(
                    "Table hints cannot be applied to FromSql, SqlQuery, or table-valued function sources in this " +
                    "query. Those rows would be read without the hint.");
            }
        }
        finally
        {
            _tableHints = null;
            _tableHintSourceRequiresTable = false;
        }

        if (queryHints.Count > 0)
        {
            Sql.AppendLine()
                .Append("OPTION (")
                .Append(string.Join(", ", queryHints))
                .Append(")");
        }
    }

    private static ISet<string> MergeTags(ISet<string> primary, ISet<string> secondary)
    {
        if (secondary.Count == 0)
        {
            return primary;
        }

        if (primary.Count == 0)
        {
            return secondary;
        }

        var merged = new HashSet<string>(primary, StringComparer.Ordinal);
        merged.UnionWith(secondary);
        return merged;
    }

    /// <inheritdoc />
    protected override void GenerateTagsHeaderComment(ISet<string> tags)
        => base.GenerateTagsHeaderComment(
            tags.Any(QueryHintTags.IsHintTag)
                ? tags.Where(tag => !QueryHintTags.IsHintTag(tag)).ToHashSet()
                : tags);

    /// <inheritdoc />
    protected override Expression VisitFromSql(FromSqlExpression fromSqlExpression)
    {
        if (_tableHints is not null)
        {
            _tableHintSourceRequiresTable = true;
        }

        return base.VisitFromSql(fromSqlExpression);
    }

    /// <inheritdoc />
    protected override Expression VisitTableValuedFunction(TableValuedFunctionExpression tableValuedFunctionExpression)
    {
        if (_tableHints is not null)
        {
            _tableHintSourceRequiresTable = true;
        }

        return base.VisitTableValuedFunction(tableValuedFunctionExpression);
    }

    /// <inheritdoc />
    protected override Expression VisitExtension(Expression extensionExpression)
    {
        var result = base.VisitExtension(extensionExpression);

        // Ordinary tables are written by VisitTable and temporal tables by the SQL Server VisitExtension override.
        // Both end with the table alias, which is where SQL Server expects table hints.
        if (_tableHints is not null && extensionExpression is TableExpression)
        {
            Sql.Append(" WITH (")
                .Append(_tableHints)
                .Append(")");
        }

        return result;
    }
}

#pragma warning restore EF1001
