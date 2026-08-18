using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests
{
    public class DbContextOptionsBuilderExtensionsTest
    {
        [Fact]
        public void UseEntityFrameworkCoreExtensionsRegistersNecessaryServices()
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestContext>().UseSqlServer("fake");

            var result = optionsBuilder.UseEntityFrameworkCoreExtensions();
            using var testContext = new TestContext(optionsBuilder.Options);
            var context = (IInfrastructure<IServiceProvider>)testContext;

            var migrationSqlGenerator = context.GetService<IMigrationsSqlGenerator>();
            var relationalAnnotationProvider = context.GetService<IRelationalAnnotationProvider>();

            Assert.Same(optionsBuilder, result);
            Assert.IsType<ExtendedSqlServerMigrationsSqlGenerator>(migrationSqlGenerator);
            Assert.IsType<ExtendedSqlServerAnnotationProvider>(relationalAnnotationProvider);
        }

        [Fact]
        public void UseEntityFrameworkCoreExtensionsCanBeCalledBeforeUseSqlServer()
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestContext>();

            var result = optionsBuilder.UseEntityFrameworkCoreExtensions();
            optionsBuilder.UseSqlServer("fake");
            using var context = new TestContext(optionsBuilder.Options);

            Assert.Same(optionsBuilder, result);
            Assert.IsType<ExtendedSqlServerMigrationsSqlGenerator>(context.GetService<IMigrationsSqlGenerator>());
            Assert.IsType<ExtendedSqlServerAnnotationProvider>(context.GetService<IRelationalAnnotationProvider>());
        }

        [Fact]
        public void UseEntityFrameworkCoreExtensionsRejectsNullBuilder()
        {
            DbContextOptionsBuilder builder = null!;

            Assert.Throws<ArgumentNullException>(() => builder.UseEntityFrameworkCoreExtensions());
        }

        [Fact]
        public async Task UseEntityFrameworkCoreExtensionsIsANoOpForInMemoryConfiguredBeforeOnConfiguring()
        {
            var options = new DbContextOptionsBuilder<ProviderSwappableContext>()
                .UseInMemoryDatabase(nameof(UseEntityFrameworkCoreExtensionsIsANoOpForInMemoryConfiguredBeforeOnConfiguring))
                .Options;
            await using var context = new ProviderSwappableContext(options);

            _ = context.Model;
            context.Database.MigrateIfSupported();
            await context.Database.MigrateIfSupportedAsync(Xunit.TestContext.Current.CancellationToken);

            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
        }

        [Fact]
        public void UseEntityFrameworkCoreExtensionsDefersMissingProviderFailureUntilContextInitialization()
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestContext>();

            var result = optionsBuilder.UseEntityFrameworkCoreExtensions();
            var exception = Assert.Throws<InvalidOperationException>(() => new TestContext(optionsBuilder.Options));

            Assert.Same(optionsBuilder, result);
            Assert.Contains("provider", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UseEntityFrameworkCoreExtensionsRejectsAnotherRelationalProviderAfterConfiguration()
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestContext>();
            optionsBuilder.UseEntityFrameworkCoreExtensions();
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(new OtherRelationalOptionsExtension());

            var exception = Assert.Throws<InvalidOperationException>(() => new TestContext(optionsBuilder.Options));

            Assert.Contains("SQL Server", exception.Message, StringComparison.Ordinal);
        }

        private sealed class ProviderSwappableContext(DbContextOptions<ProviderSwappableContext> options)
            : DbContext(options)
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                if (!optionsBuilder.IsConfigured)
                {
                    optionsBuilder.UseSqlServer("fake");
                }

                optionsBuilder.UseEntityFrameworkCoreExtensions();
            }
        }

        private sealed class OtherRelationalOptionsExtension : RelationalOptionsExtension
        {
            private readonly DbContextOptionsExtensionInfo _info;

            public OtherRelationalOptionsExtension()
            {
                _info = new ExtensionInfo(this);
            }

            private OtherRelationalOptionsExtension(OtherRelationalOptionsExtension copyFrom)
                : base(copyFrom)
            {
                _info = new ExtensionInfo(this);
            }

            public override DbContextOptionsExtensionInfo Info => _info;

            protected override RelationalOptionsExtension Clone()
                => new OtherRelationalOptionsExtension(this);

            public override void ApplyServices(IServiceCollection services)
            {
            }

            private sealed class ExtensionInfo(IDbContextOptionsExtension extension)
                : RelationalExtensionInfo(extension)
            {
                public override string LogFragment => string.Empty;

                public override int GetServiceProviderHashCode() => 0;

                public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                    => debugInfo["OtherRelational:Enabled"] = "1";

                public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
                    => other is ExtensionInfo;
            }
        }
    }
}
