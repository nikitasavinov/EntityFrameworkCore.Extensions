namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Carries query and table hints from LINQ to SQL generation as query tags, and validates those tags.
/// </summary>
internal static class QueryHintTags
{
    public const string QueryHintPrefix = "EntityFrameworkCore.Extensions:QueryHint:";
    public const string TableHintPrefix = "EntityFrameworkCore.Extensions:TableHint:";
    public const char Separator = '\u001f';

    public static string CreateQueryHints(IReadOnlyList<string> hints)
        => QueryHintPrefix + string.Join(Separator, hints);

    public static string CreateTableHints(IReadOnlyList<string> hints)
        => TableHintPrefix + string.Join(Separator, hints);

    public static bool IsHintTag(string tag)
        => tag.StartsWith(QueryHintPrefix, StringComparison.Ordinal)
            || tag.StartsWith(TableHintPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Returns the SQL of the hints carried by <paramref name="tags" />. Any tag that uses a hint prefix must contain
    /// a hint produced by <see cref="QueryHint" /> or <see cref="TableHint" />, because the SQL is written unquoted.
    /// </summary>
    public static (IReadOnlyList<string> QueryHints, IReadOnlyList<string> TableHints) Parse(IEnumerable<string> tags)
    {
        var queryPayloads = new List<string>();
        var tablePayloads = new List<string>();
        foreach (var tag in tags)
        {
            if (tag.StartsWith(QueryHintPrefix, StringComparison.Ordinal))
            {
                queryPayloads.Add(tag[QueryHintPrefix.Length..]);
            }
            else if (tag.StartsWith(TableHintPrefix, StringComparison.Ordinal))
            {
                tablePayloads.Add(tag[TableHintPrefix.Length..]);
            }
        }

        // Tags live in a hash set, so markers from separate calls have no defined order. Sorting the payloads makes
        // the SQL stable. Hints inside one call stay in the order they were given.
        queryPayloads.Sort(StringComparer.Ordinal);
        tablePayloads.Sort(StringComparer.Ordinal);

        var queryHints = HintCompatibility.Dedupe(Expand(
            QueryHintPrefix,
            queryPayloads,
            static sql => QueryHint.TryParse(sql, out var hint) ? hint.Sql : null,
            "query"));
        var tableHints = HintCompatibility.Dedupe(Expand(
            TableHintPrefix,
            tablePayloads,
            static sql => TableHint.TryParse(sql, out var hint) ? hint.Sql : null,
            "table"));
        HintCompatibility.EnsureQueryHints(queryHints);
        HintCompatibility.EnsureTableHints(tableHints);
        return (queryHints, tableHints);
    }

    private static List<string> Expand(string prefix, List<string> payloads, Func<string, string?> parse, string kind)
    {
        var hints = new List<string>();
        foreach (var payload in payloads)
        {
            if (payload.Length == 0)
            {
                throw new InvalidOperationException($"The query tag '{prefix}' does not contain a supported SQL Server {kind} hint.");
            }

            foreach (var part in payload.Split(Separator))
            {
                var sql = parse(part);
                if (sql is null)
                {
                    throw new InvalidOperationException(
                        $"The query tag '{prefix}{part}' does not contain a supported SQL Server {kind} hint.");
                }

                hints.Add(sql);
            }
        }

        return hints;
    }
}
