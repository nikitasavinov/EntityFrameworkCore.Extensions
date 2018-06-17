# EntityFrameworkCore.Extensions

Please refer to EntityFrameworkCore.Extensions.Samples for usage examples

# Build status
[![Build status](https://ci.appveyor.com/api/projects/status/o1ljbar2tvkt5w7k?svg=true)](https://ci.appveyor.com/project/nikitasavinov/entityframeworkcore-extensions)

# Examples

``` csharp
public class SampleContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ThrowOnQueryClientEvaluation(); //Throw when can not generate a query instead of loading everything into memory
        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, ExtendedMigrationSqlServerGenerator>(); //Add the support for DynamicDataMasking
        optionsBuilder.ReplaceService<IMigrationsAnnotationProvider, ExtendedSqlServerMigrationsAnnotationProvider>();

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //Set Cascade as a default delete behaviour
        modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade); 
        
         //Add dynamic data masking (https://docs.microsoft.com/en-us/sql/relational-databases/security/dynamic-data-masking)
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

    //Another way to add DynamicDataMask
    [DataMasking(MaskingFunction = "default()")]
    public string Name { get; set; } 
}

static void Main(string[] args)
{
    using (var context = new SampleContext())
    {
        context.Database.MigrateIfSupported(); //Will not throw when UseInMemoryDatabase is used 

        //Will throw instead of loading everything into memory
        var customers = context.Customers.Where(t => SomeUnsupportedFunction(t.Phone)).ToList(); 
    }
}
```
