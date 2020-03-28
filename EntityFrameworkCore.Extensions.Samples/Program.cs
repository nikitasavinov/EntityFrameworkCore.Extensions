using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Extensions.Samples
{
    class Program
    {
        public class SampleContext : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                if (!optionsBuilder.IsConfigured)
                {
                    optionsBuilder.UseSqlServer("Data Source =.; Initial Catalog = EntityFrameworkCoreExtensionsSamples; Integrated Security = True;");
                }
                optionsBuilder.UseEntityFrameworkCoreExtensions();

                base.OnConfiguring(optionsBuilder);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade); //Set Cascade as a default delete behaviour
                
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
                context.Database.MigrateIfSupported(); //Will not throw when UseInMemoryDatabase is used 

                //Add a customer
                var customer = new Customer
                {
                    Phone = "+12345678",
                    Surname = "TestCustomer",
                    DiscountCardNumber = 12881234,
                    Orders = new List<Order>
                    {
                        new Order
                        {
                            Created = DateTime.UtcNow.AddDays(-1)
                        },
                        new Order
                        {
                            Created = DateTime.UtcNow.AddDays(-10)
                        }
                    }
                };
                context.Customers.Add(customer);
                context.SaveChanges();
            }
        }
    }
}
