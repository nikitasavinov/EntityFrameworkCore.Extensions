using System;
using System.Collections.Generic;
using System.Linq;
using EntityFrameworkCore.Extensions.DynamicDataMasking;
using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

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
                optionsBuilder.ReplaceService<IMigrationsSqlGenerator, ExtendedMigrationSqlServerGenerator>(); //Add the support for DynamicDataMasking
                optionsBuilder.ReplaceService<IMigrationsAnnotationProvider, ExtendedSqlServerMigrationsAnnotationProvider>();
                optionsBuilder.ThrowOnQueryClientEvaluation(); //Throw when can not generate a query instead of loading everything into memory

                base.OnConfiguring(optionsBuilder);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                modelBuilder.OverrideDeleteBehaviour(DeleteBehavior.Cascade); //Set Cascade as a default delete behaviour
                
                modelBuilder.Entity<Customer>().Property(t => t.Surname).HasAnnotation(ExtendedAnnotationConstants.DynamicDataMasking, MaskingFunctions.Default());
                modelBuilder.Entity<Customer>().Property(t => t.DiscountCardNumber).HasAnnotation(ExtendedAnnotationConstants.DynamicDataMasking, MaskingFunctions.Random(10, 100));
                modelBuilder.Entity<Customer>().Property(t => t.Phone).HasAnnotation(ExtendedAnnotationConstants.DynamicDataMasking, MaskingFunctions.Partial(2, "XX-XX", 1));
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

            using (var context = new SampleContext())
            {
                //var customers = context.Customers.Where(t => SomeUnsupportedFunction(t.Phone)).ToList(); - Will throw instead of loading everything into memory

                var customer = context.Customers.First();
                context.Customers.Remove(customer);
                context.SaveChanges();

                context.Database.EnsureDeleted();
            }
        }

        private static bool SomeUnsupportedFunction(string input)
        {
            return input.Length == 2;
        }
    }
}
