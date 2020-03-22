using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests
{
    public class DatabaseFacadeExtensionsTests
    {
        [Fact]
        public void MigrateIfSupported_should_not_fail_for_inmemory()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseInMemoryDatabase("MigrateIfSupported_should_not_fail_for_inmemory")
                .Options;

            var context = new TestContext(options);

            context.Database.MigrateIfSupported();
            Assert.Throws<InvalidOperationException>(() => context.Database.Migrate());
        }

        [Fact]
        public async Task MigrateIfSupportedAsync_should_not_fail_for_inmemory()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseInMemoryDatabase("MigrateIfSupported_should_not_fail_for_inmemory")
                .Options;

            var context = new TestContext(options);

            context.Database.MigrateIfSupported();
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.MigrateAsync());
        }

        [Fact]
        public void MigrateIfSupported_should_migrate_for_sqlite()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseSqlite("DataSource=:memory:")
                .ReplaceService<IMigrator, MockMigrator>()
                .Options;

            var context = new TestContext(options);
            
            context.Database.MigrateIfSupported();
            var migrator = context.GetService<IMigrator>() as MockMigrator;
            Assert.True(migrator?.MigrateCalled ?? false);
        }

        [Fact]
        public async Task MigrateIfSupportedAsync_should_migrate_for_sqlite()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseSqlite("DataSource=:memory:")
                .ReplaceService<IMigrator, MockMigrator>()
                .Options;

            var context = new TestContext(options);

            await context.Database.MigrateIfSupportedAsync();
            var migrator = context.GetService<IMigrator>() as MockMigrator;
            Assert.True(migrator?.MigrateAsyncCalled ?? false);
        }

        public class MockMigrator : IMigrator
        {
            public bool MigrateCalled { get; private set; }
            public bool MigrateAsyncCalled { get; private set; }

            public void Migrate(string targetMigration = null)
            {
                MigrateCalled = true;
            }

            public Task MigrateAsync(string targetMigration = null, CancellationToken cancellationToken = new CancellationToken())
            {
                MigrateAsyncCalled = true;
                return Task.CompletedTask;
            }

            public string GenerateScript(string fromMigration = null, string toMigration = null, bool idempotent = false)
            {
                throw new NotImplementedException();
            }
        }
    }
}
