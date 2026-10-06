using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.Extensions.Tests;

internal sealed class QueryHintTestContext : DbContext
{
    public QueryHintTestContext(string? connectionString = null, params IInterceptor[] interceptors)
        : base(new DbContextOptionsBuilder<QueryHintTestContext>()
            .UseSqlServer(connectionString ?? "Server=(localdb)\\mssqllocaldb;Database=NotUsed")
            .UseEntityFrameworkCoreExtensions()
            .AddInterceptors(interceptors)
            .Options)
    {
    }

    public QueryHintTestContext(DbContextOptions<QueryHintTestContext> options)
        : base(options)
    {
    }

    public DbSet<HintOrder> Orders => Set<HintOrder>();

    public DbSet<HintOrderLine> OrderLines => Set<HintOrderLine>();

    public DbSet<HintAuditEntry> AuditEntries => Set<HintAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<HintOrder>();
        order.ToTable("Orders", "sales");
        order.Property(entity => entity.Amount).HasPrecision(18, 2);
        order.HasMany(entity => entity.Lines).WithOne().HasForeignKey(line => line.OrderId);

        var line = modelBuilder.Entity<HintOrderLine>();
        line.ToTable("OrderLines", "sales");
        line.Property(entity => entity.Product).HasMaxLength(100);

        modelBuilder.Entity<HintAuditEntry>().ToTable("AuditEntries", "sales", table => table.IsTemporal());
    }
}

internal sealed class HintOrder
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public List<HintOrderLine> Lines { get; } = [];
}

internal sealed class HintOrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

internal sealed class HintAuditEntry
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
}

internal static class QueryHintCatalog
{
    public static IReadOnlyList<(QueryHint Hint, string Sql)> QueryHints { get; } =
    [
        (QueryHint.Recompile(), "RECOMPILE"),
        (QueryHint.MaxDop(0), "MAXDOP 0"),
        (QueryHint.MaxDop(4), "MAXDOP 4"),
        (QueryHint.MaxRecursion(32_767), "MAXRECURSION 32767"),
        (QueryHint.OptimizeForUnknown(), "OPTIMIZE FOR UNKNOWN"),
        (QueryHint.ForceOrder(), "FORCE ORDER"),
        (QueryHint.HashJoin(), "HASH JOIN"),
        (QueryHint.LoopJoin(), "LOOP JOIN"),
        (QueryHint.MergeJoin(), "MERGE JOIN"),
        (QueryHint.HashGroup(), "HASH GROUP"),
        (QueryHint.OrderGroup(), "ORDER GROUP"),
        (QueryHint.ConcatUnion(), "CONCAT UNION"),
        (QueryHint.HashUnion(), "HASH UNION"),
        (QueryHint.MergeUnion(), "MERGE UNION"),
        (QueryHint.Fast(10), "FAST 10"),
        (QueryHint.KeepPlan(), "KEEP PLAN"),
        (QueryHint.KeepFixedPlan(), "KEEPFIXED PLAN"),
        (QueryHint.RobustPlan(), "ROBUST PLAN"),
        (QueryHint.ExpandViews(), "EXPAND VIEWS"),
        (QueryHint.NoPerformanceSpool(), "NO_PERFORMANCE_SPOOL"),
        (QueryHint.IgnoreNonclusteredColumnstoreIndex(), "IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX"),
        (QueryHint.MaxGrantPercent(12.5), "MAX_GRANT_PERCENT = 12.5"),
        (QueryHint.MaxGrantPercent(0.00001), "MAX_GRANT_PERCENT = 0.00001"),
        (QueryHint.MinGrantPercent(100), "MIN_GRANT_PERCENT = 100"),
        (QueryHint.UseHint("DISABLE_PARAMETER_SNIFFING"), "USE HINT('DISABLE_PARAMETER_SNIFFING')")
    ];

    public static IReadOnlyList<(TableHint Hint, string Sql)> TableHints { get; } =
    [
        (TableHint.NoLock(), "NOLOCK"),
        (TableHint.ReadUncommitted(), "READUNCOMMITTED"),
        (TableHint.ReadCommitted(), "READCOMMITTED"),
        (TableHint.ReadCommittedLock(), "READCOMMITTEDLOCK"),
        (TableHint.RepeatableRead(), "REPEATABLEREAD"),
        (TableHint.Serializable(), "SERIALIZABLE"),
        (TableHint.HoldLock(), "HOLDLOCK"),
        (TableHint.UpdLock(), "UPDLOCK"),
        (TableHint.XLock(), "XLOCK"),
        (TableHint.RowLock(), "ROWLOCK"),
        (TableHint.PagLock(), "PAGLOCK"),
        (TableHint.TabLock(), "TABLOCK"),
        (TableHint.TabLockX(), "TABLOCKX"),
        (TableHint.ReadPast(), "READPAST"),
        (TableHint.NoWait(), "NOWAIT"),
        (TableHint.ForceSeek(), "FORCESEEK"),
        (TableHint.ForceScan(), "FORCESCAN")
    ];
}

/// <summary>
/// Records the SQL of executed commands. When <c>suppressExecution</c> is set, connections are not opened and
/// non-query commands are not sent, so bulk operations can be inspected without a database.
/// </summary>
internal sealed class SqlCaptureInterceptor(bool suppressExecution = false) : IDbCommandInterceptor, IDbConnectionInterceptor
{
    public List<string> Commands { get; } = [];

    public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => suppressExecution ? InterceptionResult.Suppress() : result;

    public ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ConnectionOpening(connection, eventData, result));

    public InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Commands.Add(command.CommandText);
        return suppressExecution ? InterceptionResult<int>.SuppressWithResult(0) : result;
    }

    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(NonQueryExecuting(command, eventData, result));

    public InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Commands.Add(command.CommandText);
        return result;
    }

    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ReaderExecuting(command, eventData, result));
}
