# EntityFrameworkCore.Extensions

[![CI](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/actions/workflows/dotnetcore.yml)
[![NuGet](https://img.shields.io/nuget/v/EntityFrameworkCore.Extensions.svg)](https://www.nuget.org/packages/EntityFrameworkCore.Extensions)

SQL Server extensions for Entity Framework Core 10 and .NET 10.

See [EntityFrameworkCore.Extensions.Samples](./EntityFrameworkCore.Extensions.Samples) for more usage examples.

## Changelog

### Upcoming

- Dynamic data masking polish: scoped `GRANT` / `REVOKE UNMASK` support and remaining alter/drop edge cases.
- Row-level security through fluent annotations and migration SQL.
- SQL Server ledger table support.
- More to come.

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
