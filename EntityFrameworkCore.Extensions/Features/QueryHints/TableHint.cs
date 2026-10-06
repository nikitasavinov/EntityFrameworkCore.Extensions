using System.Diagnostics.CodeAnalysis;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Represents a SQL Server table hint that
/// <see cref="QueryableExtensions.WithTableHints{T}(IQueryable{T}, TableHint[])" /> adds as a <c>WITH (...)</c>
/// clause to every table the statement references.
/// </summary>
/// <seealso href="https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-table" />
public sealed class TableHint
{
    private static readonly HashSet<string> SupportedHints =
    [
        "NOLOCK",
        "READUNCOMMITTED",
        "READCOMMITTED",
        "READCOMMITTEDLOCK",
        "REPEATABLEREAD",
        "SERIALIZABLE",
        "HOLDLOCK",
        "UPDLOCK",
        "XLOCK",
        "ROWLOCK",
        "PAGLOCK",
        "TABLOCK",
        "TABLOCKX",
        "READPAST",
        "NOWAIT",
        "FORCESEEK",
        "FORCESCAN"
    ];

    private TableHint(string sql)
    {
        Sql = sql;
    }

    internal string Sql { get; }

    /// <summary>Creates the <c>NOLOCK</c> hint, which reads uncommitted data without taking shared locks.</summary>
    /// <returns><c>NOLOCK</c>.</returns>
    public static TableHint NoLock() => new("NOLOCK");

    /// <summary>Creates the <c>READUNCOMMITTED</c> hint, which is equivalent to <c>NOLOCK</c>.</summary>
    /// <returns><c>READUNCOMMITTED</c>.</returns>
    public static TableHint ReadUncommitted() => new("READUNCOMMITTED");

    /// <summary>Creates the <c>READCOMMITTED</c> hint, which reads with the read committed isolation level.</summary>
    /// <returns><c>READCOMMITTED</c>.</returns>
    public static TableHint ReadCommitted() => new("READCOMMITTED");

    /// <summary>Creates the <c>READCOMMITTEDLOCK</c> hint, which reads committed data with locking instead of row versioning.</summary>
    /// <returns><c>READCOMMITTEDLOCK</c>.</returns>
    public static TableHint ReadCommittedLock() => new("READCOMMITTEDLOCK");

    /// <summary>Creates the <c>REPEATABLEREAD</c> hint, which reads with the repeatable read isolation level.</summary>
    /// <returns><c>REPEATABLEREAD</c>.</returns>
    public static TableHint RepeatableRead() => new("REPEATABLEREAD");

    /// <summary>Creates the <c>SERIALIZABLE</c> hint, which reads with the serializable isolation level.</summary>
    /// <returns><c>SERIALIZABLE</c>.</returns>
    public static TableHint Serializable() => new("SERIALIZABLE");

    /// <summary>Creates the <c>HOLDLOCK</c> hint, which is equivalent to <c>SERIALIZABLE</c>.</summary>
    /// <returns><c>HOLDLOCK</c>.</returns>
    public static TableHint HoldLock() => new("HOLDLOCK");

    /// <summary>
    /// Creates the <c>UPDLOCK</c> hint, which takes update locks that are held until the transaction completes.
    /// Use it inside a transaction to lock rows for a later update.
    /// </summary>
    /// <returns><c>UPDLOCK</c>.</returns>
    public static TableHint UpdLock() => new("UPDLOCK");

    /// <summary>Creates the <c>XLOCK</c> hint, which takes exclusive locks that are held until the transaction completes.</summary>
    /// <returns><c>XLOCK</c>.</returns>
    public static TableHint XLock() => new("XLOCK");

    /// <summary>Creates the <c>ROWLOCK</c> hint, which takes row locks instead of page or table locks.</summary>
    /// <returns><c>ROWLOCK</c>.</returns>
    public static TableHint RowLock() => new("ROWLOCK");

    /// <summary>Creates the <c>PAGLOCK</c> hint, which takes page locks.</summary>
    /// <returns><c>PAGLOCK</c>.</returns>
    public static TableHint PagLock() => new("PAGLOCK");

    /// <summary>Creates the <c>TABLOCK</c> hint, which takes a table-level lock.</summary>
    /// <returns><c>TABLOCK</c>.</returns>
    public static TableHint TabLock() => new("TABLOCK");

    /// <summary>Creates the <c>TABLOCKX</c> hint, which takes an exclusive table-level lock.</summary>
    /// <returns><c>TABLOCKX</c>.</returns>
    public static TableHint TabLockX() => new("TABLOCKX");

    /// <summary>Creates the <c>READPAST</c> hint, which skips rows locked by other transactions.</summary>
    /// <returns><c>READPAST</c>.</returns>
    public static TableHint ReadPast() => new("READPAST");

    /// <summary>Creates the <c>NOWAIT</c> hint, which fails immediately instead of waiting for a lock.</summary>
    /// <returns><c>NOWAIT</c>.</returns>
    public static TableHint NoWait() => new("NOWAIT");

    /// <summary>Creates the <c>FORCESEEK</c> hint, which allows only index seeks to access the tables.</summary>
    /// <returns><c>FORCESEEK</c>.</returns>
    public static TableHint ForceSeek() => new("FORCESEEK");

    /// <summary>Creates the <c>FORCESCAN</c> hint, which allows only scans to access the tables.</summary>
    /// <returns><c>FORCESCAN</c>.</returns>
    public static TableHint ForceScan() => new("FORCESCAN");

    /// <summary>Returns the SQL text of the hint.</summary>
    /// <returns>The SQL text of the hint.</returns>
    public override string ToString() => Sql;

    /// <summary>
    /// Recreates a hint from its SQL text. Only text produced by this class's factory methods is accepted, so query
    /// tags that do not come from <see cref="QueryableExtensions" /> cannot inject SQL.
    /// </summary>
    internal static bool TryParse(string sql, [NotNullWhen(true)] out TableHint? hint)
    {
        hint = SupportedHints.Contains(sql) ? new TableHint(sql) : null;
        return hint is not null;
    }
}
