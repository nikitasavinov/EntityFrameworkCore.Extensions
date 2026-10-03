# EntityFrameworkCore.Extensions

[![CI](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/EntityFrameworkCore.Extensions?logo=nuget&label=downloads&color=004880)](https://www.nuget.org/packages/EntityFrameworkCore.Extensions/)

SQL Server columnstore and spatial indexes, dynamic data masking, query and table hints, and migration helpers for EF Core 10.

See [EntityFrameworkCore.Extensions.Samples](./EntityFrameworkCore.Extensions.Samples) for more usage examples.

## Features

- SQL Server dynamic data masking with migration support.
- SQL Server `geography` and `geometry` spatial indexes.
- SQL Server nonclustered columnstore indexes, including filtered indexes.
- SQL Server query hints (`OPTION`) and table hints (`WITH`).
- Model-wide delete behavior.
- SQL files in migrations.
- Provider-aware synchronous and asynchronous migrations.
- [Upcoming] Dynamic data masking polish: scoped `GRANT` / `REVOKE UNMASK` support and remaining alter/drop edge cases.
- [Upcoming] Row-level security through fluent annotations and migration SQL.
- [Upcoming] SQL Server ledger table support.
- [Upcoming] More to come.

## Changelog

### Unreleased

- Added SQL Server query hints (`OPTION`) and table hints (`WITH`) through `WithQueryHints` and `WithTableHints`.

### 10.2.0

- Added nonclustered columnstore indexes with fluent configuration for entity and owned-entity properties.
- Added migration support for creating, changing, renaming, and dropping columnstore indexes, with validation of supported columns and index options.

### 10.1.0

- Added SQL Server auto-grid spatial indexes for `geography` and `geometry`, including bounding-box and cells-per-object options.
- Added fluent configuration for entity and owned-entity spatial indexes, with migration support.

### 10.0.0

EntityFrameworkCore.Extensions has been revived and modernized after several years:

- Updated from .NET Core 3.1 and EF Core 5 to .NET 10 and EF Core 10.
- Strengthened dynamic data masking migrations, including mask changes, removals, and safer SQL generation.
- Kept registration inert for non-relational providers such as InMemory, with migration helpers remaining no-ops.
- Added current Linux and Windows CI, package validation, and integration tests against a real SQL Server instance.

## Install

```shell
dotnet add package EntityFrameworkCore.Extensions
```

## Example

```csharp
public sealed class SampleContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("your connection string");
        }

        // 1. Register EntityFrameworkCore.Extensions services.
        optionsBuilder.UseEntityFrameworkCoreExtensions();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 2. Set Cascade as the default delete behavior.
        modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade);

        // 3. Add SQL Server dynamic data masks.
        modelBuilder.Entity<Customer>()
            .Property(customer => customer.Surname)
            .HasDataMask(MaskingFunctions.Default());

        modelBuilder.Entity<Customer>()
            .Property(customer => customer.DiscountCardNumber)
            .HasDataMask(MaskingFunctions.Random(10, 100));

        modelBuilder.Entity<Customer>()
            .Property(customer => customer.Phone)
            .HasDataMask(MaskingFunctions.Partial(2, "XX-XX", 1));
    }
}

public static class Program
{
    public static void Main()
    {
        using var context = new SampleContext();

        // 4. This is a no-op for non-relational providers such as InMemory.
        context.Database.MigrateIfSupported();
    }
}
```

## Spatial indexes

Install `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`, enable it in `UseSqlServer()`, and configure the index alongside the rest of the entity model:

```csharp
optionsBuilder.UseSqlServer(
    connectionString,
    sqlServer => sqlServer.UseNetTopologySuite());
optionsBuilder.UseEntityFrameworkCoreExtensions();

modelBuilder.Entity<Place>()
    .Property(place => place.Location)
    .HasColumnType("geography");

modelBuilder.Entity<Place>()
    .HasSpatialIndex(place => place.Location)
    .HasDatabaseName("SIX_Places_Location");

modelBuilder.Entity<Region>()
    .Property(region => region.Boundary)
    .HasColumnType("geometry");

modelBuilder.Entity<Region>()
    .HasSpatialIndex(
        region => region.Boundary,
        spatial => spatial
            .HasBoundingBox(-180, -90, 180, 90)
            .HasCellsPerObject(32))
    .HasDatabaseName("SIX_Regions_Boundary");
```

`geography` uses `GEOGRAPHY_AUTO_GRID`. `geometry` uses `GEOMETRY_AUTO_GRID` and requires a bounding box.

Each entity with a spatial index must have a primary key backed by a clustered SQL Server index. This is the SQL Server provider default; configuring the primary key with `.IsClustered(false)` is not supported. Only `.HasDatabaseName()` may be chained after `.HasSpatialIndex()`; unique, filtered, clustered, included-column, descending, and other SQL Server index options are not supported.

Use the overload with a model index name to create multiple spatial indexes on one property, and use `.HasDatabaseName()` to give each one a distinct SQL index name. The same expression-based, string-based, and named overloads are available on `OwnedNavigationBuilder`.

## Columnstore indexes

Configure a nonclustered columnstore index for reporting and aggregation queries on SQL Server 2016 or later:

```csharp
optionsBuilder.UseSqlServer(connectionString);
optionsBuilder.UseEntityFrameworkCoreExtensions();

modelBuilder.Entity<Order>().Property(order => order.Amount).HasPrecision(18, 2);
modelBuilder.Entity<Order>()
    .HasColumnstoreIndex(order => new { order.Created, order.Amount })
    .HasDatabaseName("NCCI_Orders_Reporting")
    .HasFilter("[Amount] > 0"); // Optional SQL predicate, using database column names.
```

The generated migration creates a `CREATE NONCLUSTERED COLUMNSTORE INDEX`. Changes to its selected columns or filter drop and recreate the index; renames and removal use the standard EF Core migration operations. Reverse migrations restore the previous definition. See the sample project's `AddColumnstoreIndex` migration for an example.

Use the expression overload for one property or an anonymous object containing several properties. String-based overloads accept property names, for example `.HasColumnstoreIndex("Created", "Amount")`. To retain an ordinary index on the same properties, give the columnstore index a separate **model** name: `.HasColumnstoreIndex(order => new { order.Created, order.Amount }, "Reporting")`. The same overloads are available for owned entities, with the string-based named overload taking a property-name array followed by the model index name.

SQL Server permits only one columnstore index per table, including tables shared by owned entities. This implementation supports nonclustered indexes with 1–1024 columns of supported built-in SQL Server types. Indexed strings and binary values must have bounded lengths; `varchar(max)`, `nvarchar(max)`, `varbinary(max)`, XML, spatial/CLR types, `sql_variant`, `rowversion`, computed columns, and sparse columns are unsupported. Nonclustered columnstore indexes on memory-optimized tables are also unsupported.

Only `.HasDatabaseName()` and `.HasFilter()` customize these indexes; explicit `.IsClustered(false)`, `.IsCreatedOnline(false)`, and `.SortInTempDb(false)` are also accepted. Clustered columnstore indexes, unique indexes, included columns, sort direction, ordered columnstore indexes, and additional SQL Server index options such as ONLINE or compression settings are not implemented. Invalid configurations fail during design-time model creation or migration SQL generation. Table and column checks during SQL generation require corresponding model metadata; handwritten migrations without it rely on SQL Server for those checks. See [SQL Server's columnstore documentation](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-columnstore-index-transact-sql) for database requirements and restrictions.

## Query and table hints

Add SQL Server hints to a LINQ query. `UseEntityFrameworkCoreExtensions()` registers the SQL generator:

```csharp
var orders = await context.Orders
    .Where(order => order.CustomerId == customerId)
    .Include(order => order.Lines)
    .TagWith("Orders for customer page")
    .WithQueryHints(QueryHint.Recompile(), QueryHint.MaxDop(1))
    .WithTableHints(TableHint.NoLock())
    .ToListAsync();
```

Query hints become one `OPTION` clause at the end of the statement. Table hints become a `WITH (...)` clause on every table the statement references, including joins and subqueries. Other query tags stay SQL comments and do not have to come first. The same calls apply to `ExecuteUpdate` and `ExecuteDelete`, to every command of a split query, and to hints written inside a compiled query, a correlated subquery, or a global query filter. Calling either method on an EF Core query throws when `UseEntityFrameworkCoreExtensions()` was not configured.

`QueryHint` and `TableHint` check their arguments, and combinations SQL Server does not allow on one statement (such as two `MAXDOP` values, `NOLOCK` with `ROWLOCK`, `NOLOCK` with `UPDLOCK` or `XLOCK`, or `READPAST` with `NOLOCK`, `HOLDLOCK`, or `SERIALIZABLE`) throw before a command is sent. Several `JOIN`, `UNION`, or `GROUP` strategy hints can be combined, as can `KEEP PLAN` with `KEEPFIXED PLAN`. Hints in one call are written in the order given. See [query hints](https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-query) and [table hints](https://learn.microsoft.com/sql/t-sql/queries/hints-transact-sql-table) for what SQL Server does with each one. The supported set works on SQL Server 2016 and later.

Table hints apply to every table the statement references, including each table of a join. Uncomposed `FromSql` and `SqlQuery` queries keep the SQL you wrote, so put hints in that SQL. A query that includes `FromSql`, `SqlQuery`, or a table-valued function cannot take table hints, including when that source is joined to a mapped table, because those rows would be read without the hint. A composed raw SQL query can take query hints. Locking hints such as `UPDLOCK` last only until the current transaction ends.
