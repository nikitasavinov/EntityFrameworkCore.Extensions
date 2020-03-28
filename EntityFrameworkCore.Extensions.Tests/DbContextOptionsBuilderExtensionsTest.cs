using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests
{
    public class DbContextOptionsBuilderExtensionsTest
    {
        [Fact]
        public void UseEntityFrameworkCoreExtensions_should_register_necessary_services()
        {
            var builder = new DbContextOptionsBuilder<TestContext>()
                .UseSqlServer("fake")
                .UseEntityFrameworkCoreExtensions()
                .Options;
            var context = (IInfrastructure<IServiceProvider>)new TestContext(builder);

            var migrationSqlGenerator = context.GetService<IMigrationsSqlGenerator>();
            var migrationAnnotationsProvider = context.GetService<IMigrationsAnnotationProvider>();

            Assert.IsType<ExtendedMigrationSqlServerGenerator>(migrationSqlGenerator);
            Assert.IsType<ExtendedSqlServerMigrationsAnnotationProvider>(migrationAnnotationsProvider);
        }
    }
}
