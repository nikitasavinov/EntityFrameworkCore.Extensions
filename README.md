# EntityFrameworkCore.Extensions

[![CI](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/EntityFrameworkCore.Extensions?logo=nuget&label=downloads&color=004880)](https://www.nuget.org/packages/EntityFrameworkCore.Extensions/)

SQL Server spatial indexes, dynamic data masking, and migration helpers for EF Core 10.

See [EntityFrameworkCore.Extensions.Samples](./EntityFrameworkCore.Extensions.Samples) for more usage examples.

## Features

- SQL Server dynamic data masking with migration support.
- Model-wide delete behavior.
- SQL files in migrations.
- Provider-aware synchronous and asynchronous migrations.
- [Upcoming] SQL Server `geography` and `geometry` spatial indexes.
- [Upcoming] Dynamic data masking polish: scoped `GRANT` / `REVOKE UNMASK` support and remaining alter/drop edge cases.
- [Upcoming] Row-level security through fluent annotations and migration SQL.
- [Upcoming] SQL Server ledger table support.
- [Upcoming] More to come.

## Changelog

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
