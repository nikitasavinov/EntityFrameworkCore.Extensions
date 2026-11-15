# EntityFrameworkCore.Extensions

[![CI](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/EntityFrameworkCore.Extensions?logo=nuget&label=downloads&color=004880)](https://www.nuget.org/packages/EntityFrameworkCore.Extensions/)

SQL Server features that EF Core 10 has no built-in API for, configured in the model and in LINQ:

- [Dynamic data masking](#dynamic-data-masking): `HasDataMask` masks a column, so users without the `UNMASK` permission read `+1XX-XX8` where `+12345678` is stored.
- [Spatial indexes](#spatial-indexes): `HasSpatialIndex` creates an index on a `geography` or `geometry` column, with bounding box and cells-per-object options.
- [Columnstore indexes](#columnstore-indexes): `HasColumnstoreIndex` creates a nonclustered columnstore index on the columns you select, with an optional filter.
- [Query and table hints](#query-and-table-hints): `WithQueryHints` and `WithTableHints` add `OPTION (...)` and `WITH (...)` to a LINQ query, for hints such as `RECOMPILE`, `MAXDOP`, `NOLOCK`, `UPDLOCK` and `READPAST`.
- [Helpers](#helpers): `MigrateIfSupported` for providers without migrations, `SqlFile` for SQL files in migrations, and `OverrideDeleteBehaviour` for a model-wide delete behavior.

Indexes and masks are part of the model, so migrations create, change and drop them.

## Installation

```shell
dotnet add package EntityFrameworkCore.Extensions
```

```csharp
services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseEntityFrameworkCoreExtensions());
```

- Requires EF Core 10, .NET 10 and SQL Server 2016 or later.
- `UseEntityFrameworkCoreExtensions()` can be called before or after `UseSqlServer`, and also in `OnConfiguring`.
- Sample project: [EntityFrameworkCore.Extensions.Samples](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/tree/master/EntityFrameworkCore.Extensions.Samples).

## Dynamic data masking

`HasDataMask` applies a [dynamic data masking](https://learn.microsoft.com/sql/relational-databases/security/dynamic-data-masking) function to a column. Users without the `UNMASK` permission read masked values. The stored data is unchanged.

```csharp
modelBuilder.Entity<Customer>(customer =>
{
    customer.Property(c => c.Surname).HasDataMask(MaskingFunctions.Default());
    customer.Property(c => c.Email).HasDataMask(MaskingFunctions.Email());
    customer.Property(c => c.Phone).HasDataMask(MaskingFunctions.Partial(2, "XX-XX", 1));
    customer.Property(c => c.DiscountCardNumber).HasDataMask(MaskingFunctions.Random(10, 100));
});
```

| Function | Stored value | Read without `UNMASK` |
| --- | --- | --- |
| `Default()` | `Smith` | `xxxx` |
| `Email()` | `smith@example.com` | `sXXX@XXXX.com` |
| `Partial(2, "XX-XX", 1)` | `+12345678` | `+1XX-XX8` |
| `Random(10, 100)` | `12881234` | a random number from 10 to 100 |

- Migrations add, replace and drop masks as the model changes.
- `HasDataMask` accepts any masking function as a string, for example `HasDataMask("datetime(\"Y\")")` on SQL Server 2022 or later.
- Permissions are not modeled. Grant `UNMASK` in SQL. Members of `db_owner` always read the stored values.

## Spatial indexes

`HasSpatialIndex` creates a spatial index on a `geography` or `geometry` column. It requires the `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` package and `UseNetTopologySuite()` in `UseSqlServer`.

```csharp
modelBuilder.Entity<Place>()
    .HasSpatialIndex(p => p.Location);

modelBuilder.Entity<Region>(region =>
{
    region.Property(r => r.Boundary).HasColumnType("geometry");
    region.HasSpatialIndex(
            r => r.Boundary,
            spatial => spatial.HasBoundingBox(-180, -90, 180, 90).HasCellsPerObject(32))
        .HasDatabaseName("SIX_Regions_Boundary");
});
```

```sql
CREATE SPATIAL INDEX [IX_Places_Location] ON [Places] ([Location])
USING GEOGRAPHY_AUTO_GRID;

CREATE SPATIAL INDEX [SIX_Regions_Boundary] ON [Regions] ([Boundary])
USING GEOMETRY_AUTO_GRID
WITH (BOUNDING_BOX = (-180, -90, 180, 90), CELLS_PER_OBJECT = 32);
```

- NetTopologySuite properties map to `geography` by default. Set `HasColumnType("geometry")` for `geometry`.
- A `geometry` index requires `HasBoundingBox`. A `geography` index does not accept one. `HasCellsPerObject` is optional.
- Overloads accept property names as strings, owned types, and a model index name for a second index on the same property.

## Columnstore indexes

`HasColumnstoreIndex` creates a nonclustered columnstore index with an optional filter.

```csharp
modelBuilder.Entity<Order>()
    .HasColumnstoreIndex(o => new { o.Created, o.Amount })
    .HasDatabaseName("NCCI_Orders_Reporting")
    .HasFilter("[Amount] > 0");
```

```sql
CREATE NONCLUSTERED COLUMNSTORE INDEX [NCCI_Orders_Reporting]
ON [Orders] ([Created], [Amount])
WHERE [Amount] > 0;
```

- Changing the columns or the filter drops and recreates the index. Changing the database name renames it.
- A string property without a configured length becomes `nvarchar(450)`, as for any index. Set `HasMaxLength` to control it.
- To keep a rowstore index on the same columns, pass a model index name: `.HasColumnstoreIndex(o => new { o.Created, o.Amount }, "Reporting")`.
- Overloads accept property names as strings and owned types.

## Query and table hints

`WithQueryHints` appends one `OPTION (...)` clause to the statement. `WithTableHints` adds `WITH (...)` to every table in the statement, including joined tables and subqueries. SQL Server documents each [query hint](https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-query) and [table hint](https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-table).

```csharp
var orders = await context.Orders
    .Where(o => o.CustomerId == customerId)
    .WithQueryHints(QueryHint.Recompile())
    .WithTableHints(TableHint.NoLock())
    .ToListAsync();
```

```sql
SELECT [o].[Id], [o].[Amount], [o].[Created], [o].[CustomerId]
FROM [Orders] AS [o] WITH (NOLOCK)
WHERE [o].[CustomerId] = @customerId
OPTION (RECOMPILE)
```

Locking hints, inside a transaction:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

var job = await context.Jobs
    .Where(j => j.StartedAt == null)
    .OrderBy(j => j.Id)
    .WithTableHints(TableHint.UpdLock(), TableHint.ReadPast(), TableHint.RowLock())
    .FirstOrDefaultAsync();

if (job is not null)
{
    job.StartedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();
}

await transaction.CommitAsync();
```

```sql
SELECT TOP(1) [j].[Id], [j].[Payload], [j].[StartedAt]
FROM [Jobs] AS [j] WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE [j].[StartedAt] IS NULL
ORDER BY [j].[Id]
```

- Hints apply to `Include` joins, every command of a split query, `ExecuteUpdate`, `ExecuteDelete` and temporal tables. Hint calls inside compiled queries, correlated subqueries and global query filters are supported.
- Hint arguments are validated. Combinations that SQL Server rejects, such as `NOLOCK` with `UPDLOCK` or two `MAXDOP` values, throw before a command is sent.
- Both methods throw when `UseEntityFrameworkCoreExtensions()` is not configured. The InMemory provider ignores hints.
- Locking hints such as `UPDLOCK` hold their locks until the transaction ends.

### Supported hints

**Query hints** (`QueryHint`): `RECOMPILE`, `MAXDOP`, `MAXRECURSION`, `OPTIMIZE FOR UNKNOWN`, `FORCE ORDER`, `HASH JOIN`, `LOOP JOIN`, `MERGE JOIN`, `HASH GROUP`, `ORDER GROUP`, `CONCAT UNION`, `HASH UNION`, `MERGE UNION`, `FAST`, `KEEP PLAN`, `KEEPFIXED PLAN`, `ROBUST PLAN`, `EXPAND VIEWS`, `NO_PERFORMANCE_SPOOL`, `IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX`, `MAX_GRANT_PERCENT`, `MIN_GRANT_PERCENT`, and any `USE HINT` name.

**Table hints** (`TableHint`): `NOLOCK`, `READUNCOMMITTED`, `READCOMMITTED`, `READCOMMITTEDLOCK`, `REPEATABLEREAD`, `SERIALIZABLE`, `HOLDLOCK`, `UPDLOCK`, `XLOCK`, `ROWLOCK`, `PAGLOCK`, `TABLOCK`, `TABLOCKX`, `READPAST`, `NOWAIT`, `FORCESEEK`, `FORCESCAN`.

Factory methods use the matching names, for example `QueryHint.OptimizeForUnknown()`, `QueryHint.MaxDop(4)`, `QueryHint.UseHint("DISABLE_PARAMETER_SNIFFING")` and `TableHint.ReadPast()`.

## Helpers

### MigrateIfSupported

Applies pending migrations. On a provider without migrations, such as InMemory, it does nothing, whereas `Migrate()` throws.

```csharp
await context.Database.MigrateIfSupportedAsync();
```

### SqlFile

Adds the contents of a SQL file to a migration. The file is read when the migration is applied or scripted, so it must be copied to the output directory and addressed from `AppContext.BaseDirectory`.

```csharp
migrationBuilder.SqlFile(
    Path.Combine(AppContext.BaseDirectory, "SqlFiles", "ImportCustomers.sql"));
```

```xml
<None Update="SqlFiles/ImportCustomers.sql" CopyToOutputDirectory="PreserveNewest" />
```

### OverrideDeleteBehaviour

Sets the delete behavior of every foreign key that is already in the model. Call it at the end of `OnModelCreating`. The default argument is `Restrict`.

```csharp
modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Restrict);
```

## Limitations

Unsupported model configurations are rejected when the migration is generated. Unsupported hints throw before a command is sent.

**Spatial indexes**

- The table must have a clustered primary key, which is the EF Core default.
- An index covers one column and uses auto-grid tessellation.
- Only `HasDatabaseName()` can be chained. Unique, filtered, included-column, descending and other index options are rejected.

**Columnstore indexes**

- SQL Server allows one columnstore index per table. An index can have up to 1,024 columns.
- Only nonclustered columnstore indexes are created. Clustered and ordered columnstore indexes are not available.
- Only `HasDatabaseName()` and `HasFilter()` can be chained. Uniqueness, included columns, sort order, `ONLINE` and compression are rejected.
- Supported column types are numeric, `bit`, date and time, `uniqueidentifier`, and string and binary types with a bounded length. Computed columns, sparse columns and memory-optimized tables are rejected.
- Database requirements are listed in SQL Server's [CREATE COLUMNSTORE INDEX](https://learn.microsoft.com/sql/t-sql/statements/create-columnstore-index-transact-sql) documentation.

**Query and table hints**

- Table hints apply to every table in the statement. A single table cannot be targeted.
- Index hints are not available.
- Uncomposed `FromSql` and `SqlQuery` queries reject hints. Composed ones accept query hints only. Queries over table-valued functions reject table hints.

## Compatibility

- CI runs the integration tests against SQL Server 2016 and SQL Server 2025.
- With the InMemory provider the registration is inert: the model builds, hints are ignored and `MigrateIfSupported` does nothing. Other relational providers are rejected with an `InvalidOperationException`.
- `UseEntityFrameworkCoreExtensions()` replaces six EF Core services: `IMigrationsSqlGenerator`, `IRelationalAnnotationProvider`, `IQuerySqlGeneratorFactory`, `IRelationalParameterBasedSqlProcessorFactory`, `IQueryTranslationPreprocessorFactory` and `IQueryCompiler`. If another registration replaces one of them, the last one takes effect.

## Roadmap

- `GRANT` and `REVOKE UNMASK` for dynamic data masking.
- Row-level security.
- Ledger tables.

## Release notes

[GitHub Releases](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/releases)

## License

[MIT](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/blob/master/LICENSE)
