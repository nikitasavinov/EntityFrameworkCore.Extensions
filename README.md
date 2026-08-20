# EntityFrameworkCore.Extensions

[![CI](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml)
[![NuGet downloads](https://img.shields.io/nuget/dt/EntityFrameworkCore.Extensions?logo=nuget&label=downloads&color=004880)](https://www.nuget.org/packages/EntityFrameworkCore.Extensions/)

SQL Server extensions for Entity Framework Core 10 and .NET 10.

See [EntityFrameworkCore.Extensions.Samples](./EntityFrameworkCore.Extensions.Samples) for more usage examples.

## Features

This project extends EFCore with a few major features:
- SQL Server dynamic data masking configured through the EF Core fluent API, with migrations for adding, changing, and removing masks.
- SQL Server `geography` and `geometry` spatial indexes with auto-grid tessellation, bounding-box configuration, and cells-per-object configuration.

And some minor ones too:
- Default delete-behavior overrides for an entire EF Core model.
- SQL migration scripts loaded from external files.
- Safe synchronous and asynchronous migration helpers that remain no-ops for non-relational providers such as InMemory.

## Changelog

### Upcoming

- More to come.

### 10.0.0

EntityFrameworkCore.Extensions has been revived and modernized after several years:

- Updated from .NET Core 3.1 and EF Core 5 to .NET 10 and EF Core 10.
- Strengthened dynamic data masking migrations, including mask changes, removals, and safer SQL generation.
- Kept registration inert for non-relational providers such as InMemory, with migration helpers remaining no-ops.
- Added current Linux and Windows CI, package validation, and integration tests against a real SQL Server instance.
- Added SQL Server `geography` and `geometry` spatial indexes with auto-grid tessellation, fluent configuration, and migrations.
- Added bounding-box and cells-per-object configuration with validation for unsupported spatial-index shapes and options.
- Added sample models and a migration demonstrating both spatial data types.
- Added live SQL Server verification of spatial-index metadata, queries, and execution plans.
- Reorganized the library and tests into feature-oriented folders for future extensions.

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
    .HasSpatialIndex(place => place.Location)
    .HasDatabaseName("SIX_Places_Location");

modelBuilder.Entity<Region>()
    .HasSpatialIndex(
        region => region.Boundary,
        spatial => spatial
            .HasBoundingBox(-180, -90, 180, 90)
            .HasCellsPerObject(32))
    .HasDatabaseName("SIX_Regions_Boundary");
```

`geography` uses `GEOGRAPHY_AUTO_GRID`. `geometry` uses `GEOMETRY_AUTO_GRID` and requires a bounding box.
