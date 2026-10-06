using System.Globalization;

namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Rejects hint combinations that SQL Server does not allow on one statement.
/// </summary>
internal static class HintCompatibility
{
    // SQL Server allows at most one hint from each group. NOLOCK and READUNCOMMITTED belong to both.
    // https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-table
    private static readonly HashSet<string> GranularityHints = new(StringComparer.Ordinal)
    {
        "PAGLOCK",
        "NOLOCK",
        "READUNCOMMITTED",
        "READCOMMITTEDLOCK",
        "ROWLOCK",
        "TABLOCK",
        "TABLOCKX"
    };

    private static readonly HashSet<string> IsolationHints = new(StringComparer.Ordinal)
    {
        "HOLDLOCK",
        "NOLOCK",
        "READUNCOMMITTED",
        "READCOMMITTED",
        "REPEATABLEREAD",
        "SERIALIZABLE"
    };

    // Msg 1047. These two name a lock mode, so a query can include only one of them.
    private static readonly HashSet<string> LockModeHints = new(StringComparer.Ordinal)
    {
        "UPDLOCK",
        "XLOCK"
    };

    public static List<string> Dedupe(IReadOnlyList<string> hints)
    {
        var result = new List<string>(hints.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hint in hints)
        {
            if (seen.Add(hint))
            {
                result.Add(hint);
            }
        }

        return result;
    }

    public static void EnsureQueryHints(IReadOnlyList<string> hints)
    {
        string? maxDop = null;
        string? maxRecursion = null;
        string? fast = null;
        string? minimumGrantSql = null;
        string? maximumGrantSql = null;
        double? minimumGrant = null;
        double? maximumGrant = null;
        foreach (var hint in hints)
        {
            // SQL Server accepts several JOIN, UNION, or GROUP strategies and chooses among them.
            // KEEP PLAN and KEEPFIXED PLAN can also be combined.
            maxDop = TakeSingleton(hint, "MAXDOP ", maxDop, "MAXDOP");
            maxRecursion = TakeSingleton(hint, "MAXRECURSION ", maxRecursion, "MAXRECURSION");
            fast = TakeSingleton(hint, "FAST ", fast, "FAST");
            minimumGrantSql = TakeSingleton(hint, "MIN_GRANT_PERCENT = ", minimumGrantSql, "MIN_GRANT_PERCENT");
            maximumGrantSql = TakeSingleton(hint, "MAX_GRANT_PERCENT = ", maximumGrantSql, "MAX_GRANT_PERCENT");

            if (TryReadGrant(hint, "MIN_GRANT_PERCENT", out var minimum))
            {
                minimumGrant = minimum;
            }
            else if (TryReadGrant(hint, "MAX_GRANT_PERCENT", out var maximum))
            {
                maximumGrant = maximum;
            }
        }

        if (minimumGrant is double minimumValue
            && maximumGrant is double maximumValue
            && minimumValue > maximumValue)
        {
            throw new InvalidOperationException(
                $"MIN_GRANT_PERCENT ({Format(minimumValue)}) is greater than MAX_GRANT_PERCENT ({Format(maximumValue)}).");
        }
    }

    public static void EnsureTableHints(IReadOnlyList<string> hints)
    {
        string? granularity = null;
        string? isolation = null;
        string? access = null;
        string? lockMode = null;
        string? dirtyRead = null;
        string? readPast = null;
        foreach (var hint in hints)
        {
            granularity = TakeExclusive(GranularityHints, hint, granularity, "granularity table");
            isolation = TakeExclusive(IsolationHints, hint, isolation, "isolation level table");
            lockMode = TakeExclusive(LockModeHints, hint, lockMode, "locking table");
            if (hint is "FORCESEEK" or "FORCESCAN")
            {
                if (access is not null)
                {
                    ThrowExclusive("access table", access, hint);
                }

                access = hint;
            }

            // READUNCOMMITTED is the same hint as NOLOCK. Both conflict with a requested lock (Msg 1047).
            if (hint is "NOLOCK" or "READUNCOMMITTED")
            {
                dirtyRead ??= hint;
            }
            else if (hint == "READPAST")
            {
                readPast = hint;
            }
        }

        if (dirtyRead is not null && lockMode is not null)
        {
            ThrowIncompatible(dirtyRead, lockMode);
        }

        // READPAST works only at READ COMMITTED or REPEATABLE READ (Msg 650).
        if (readPast is not null && isolation is "NOLOCK" or "READUNCOMMITTED" or "HOLDLOCK" or "SERIALIZABLE")
        {
            ThrowIncompatible(isolation, readPast);
        }
    }

    private static string? TakeSingleton(string hint, string prefix, string? current, string kind)
    {
        if (!hint.StartsWith(prefix, StringComparison.Ordinal))
        {
            return current;
        }

        if (current is not null && !string.Equals(current, hint, StringComparison.Ordinal))
        {
            ThrowExclusive(kind, current, hint);
        }

        return hint;
    }

    private static string? TakeExclusive(HashSet<string> group, string hint, string? current, string kind)
    {
        if (!group.Contains(hint))
        {
            return current;
        }

        if (current is not null)
        {
            ThrowExclusive(kind, current, hint);
        }

        return hint;
    }

    private static bool TryReadGrant(string hint, string name, out double value)
    {
        var prefix = name + " = ";
        if (!hint.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = 0;
            return false;
        }

        return double.TryParse(
            hint[prefix.Length..],
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string Format(double value)
        => value.ToString("0.###############", CultureInfo.InvariantCulture);

    private static void ThrowExclusive(string kind, string first, string second)
        => throw new InvalidOperationException(
            $"SQL Server allows only one {kind} hint. The query includes {first} and {second}.");

    private static void ThrowIncompatible(string first, string second)
        => throw new InvalidOperationException(
            $"SQL Server does not allow the table hints {first} and {second} together.");
}
