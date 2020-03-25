# EntityFrameworkCore.Extensions

Please refer to EntityFrameworkCore.Extensions.Samples for usage examples

# Build status
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/24d129322030411ba52247aa7c9b0bbf)](https://app.codacy.com/app/nikitasavinov/EntityFrameworkCore.Extensions?utm_source=github.com&utm_medium=referral&utm_content=nikitasavinov/EntityFrameworkCore.Extensions&utm_campaign=Badge_Grade_Dashboard)
[![NugetPackage](https://buildstats.info/nuget/EntityFrameworkCore.Extensions)](https://buildstats.info/nuget/EntityFrameworkCore.Extensions)

# Examples

``` csharp
public class SampleContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //1. Throw when can not generate a query instead of loading everything into memory
        optionsBuilder.ThrowOnQueryClientEvaluation(); 
        
        //2. Add the support for DynamicDataMasking
        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, ExtendedMigrationSqlServerGenerator>();
        optionsBuilder.ReplaceService<IMigrationsAnnotationProvider, ExtendedSqlServerMigrationsAnnotationProvider>();

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //3. Set Cascade as a default delete behaviour
        modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade); 
        
         //4. Add dynamic data masking (https://docs.microsoft.com/en-us/sql/relational-databases/security/dynamic-data-masking)
        modelBuilder.Entity<Customer>()
            .Property(t => t.Surname)
            .HasAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Default());
        modelBuilder.Entity<Customer>()
            .Property(t => t.DiscountCardNumber)
            .HasAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Random(10, 100));
        modelBuilder.Entity<Customer>()
            .Property(t => t.Phone)
            .HasAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Partial(2, "XX-XX", 1));
    }

    public DbSet<Customer> Customers { get; set; }
}
    
public class Customer
{
    public int Id { get; set; }

    //5. Another way to add DynamicDataMask
    [DataMasking(MaskingFunction = "default()")]
    public string Name { get; set; } 
}

static void Main(string[] args)
{
    using (var context = new SampleContext())
    {
        //6. Will not throw when UseInMemoryDatabase is used 
        context.Database.MigrateIfSupported(); 

        //7. Will throw instead of loading everything into memory
        var customers = context.Customers.Where(t => SomeUnsupportedFunction(t.Phone)).ToList(); 
    }
}
```
