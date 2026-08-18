using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Extensions.Samples;

internal sealed class Program
{
    public sealed class SampleContext : DbContext
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(
                    "Data Source=.;Initial Catalog=EntityFrameworkCoreExtensionsSamples;Integrated Security=True;TrustServerCertificate=True");
            }

            optionsBuilder.UseEntityFrameworkCoreExtensions();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade);

            modelBuilder.Entity<Customer>().Property(customer => customer.Surname).HasDataMask(MaskingFunctions.Default());
            modelBuilder.Entity<Customer>().Property(customer => customer.DiscountCardNumber).HasDataMask(MaskingFunctions.Random(10, 100));
            modelBuilder.Entity<Customer>().Property(customer => customer.Phone).HasDataMask(MaskingFunctions.Partial(2, "XX-XX", 1));
        }
    }

    private static void Main()
    {
        using var context = new SampleContext();
        context.Database.MigrateIfSupported();

        var customer = new Customer
        {
            Phone = "+12345678",
            Surname = "TestCustomer",
            DiscountCardNumber = 12881234,
            Orders = new List<Order>
            {
                new()
                {
                    Created = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    Created = DateTime.UtcNow.AddDays(-10)
                }
            }
        };

        context.Customers.Add(customer);
        context.SaveChanges();
    }
}
