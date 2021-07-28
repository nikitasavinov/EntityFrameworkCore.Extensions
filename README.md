# EntityFrameworkCore.Extensions

Please refer to EntityFrameworkCore.Extensions.Samples for usage examples.
EF Core 5.x support is in beta: https://www.nuget.org/packages/EntityFrameworkCore.Extensions/5.0.0-beta

# Build status
![Build](https://github.com/nikitasavinov/EntityFrameworkCore.Extensions/workflows/Build/badge.svg)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/24d129322030411ba52247aa7c9b0bbf)](https://app.codacy.com/app/nikitasavinov/EntityFrameworkCore.Extensions?utm_source=github.com&utm_medium=referral&utm_content=nikitasavinov/EntityFrameworkCore.Extensions&utm_campaign=Badge_Grade_Dashboard)
[![NugetPackage](https://buildstats.info/nuget/EntityFrameworkCore.Extensions)](https://www.nuget.org/packages/EntityFrameworkCore.Extensions)

# Examples

``` csharp
public class SampleContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //1. Use EntityFrameworkCoreExtensions (add DynamicDataMasking support)
        optionsBuilder.UseEntityFrameworkCoreExtensions();

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //2. Set Cascade as a default delete behaviour
        modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade); 
        
        //3. Add dynamic data masking (https://docs.microsoft.com/en-us/sql/relational-databases/security/dynamic-data-masking)
        modelBuilder.Entity<Customer>().Property(t => t.Surname).HasDataMask(MaskingFunctions.Default());
        modelBuilder.Entity<Customer>().Property(t => t.DiscountCardNumber).HasDataMask(MaskingFunctions.Random(10, 100));
        modelBuilder.Entity<Customer>().Property(t => t.Phone).HasDataMask(MaskingFunctions.Partial(2, "XX-XX", 1));
    }

    public DbSet<Customer> Customers { get; set; }
}

static void Main(string[] args)
{
    using (var context = new SampleContext())
    {
        //4. Will not throw when UseInMemoryDatabase is used 
        context.Database.MigrateIfSupported();
    }
}
```
