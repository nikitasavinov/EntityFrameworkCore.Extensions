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
        public void MigrateIfSupportedDoesNotFailForInMemory()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseInMemoryDatabase(nameof(MigrateIfSupportedDoesNotFailForInMemory))
                .Options;

            using var context = new TestContext(options);

            context.Database.MigrateIfSupported();
            Assert.Throws<InvalidOperationException>(() => context.Database.Migrate());
        }

        [Fact]
        public async Task MigrateIfSupportedAsyncDoesNotFailForInMemory()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseInMemoryDatabase(nameof(MigrateIfSupportedAsyncDoesNotFailForInMemory))
                .Options;

            await using var context = new TestContext(options);

            Func<DatabaseFacade, Task> migrateIfSupportedAsync = DatabaseFacadeExtensions.MigrateIfSupportedAsync;
            await migrateIfSupportedAsync(context.Database);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Database.MigrateAsync(Xunit.TestContext.Current.CancellationToken));
        }

        [Fact]
        public void MigrateIfSupportedMigratesForRelationalProvider()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NotUsed")
                .ReplaceService<IMigrator, MockMigrator>()
                .Options;

            using var context = new TestContext(options);

            context.Database.MigrateIfSupported();
            var migrator = context.GetService<IMigrator>() as MockMigrator;
            Assert.True(migrator?.MigrateCalled ?? false);
        }

        [Fact]
        public async Task MigrateIfSupportedAsyncMigratesForRelationalProvider()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NotUsed")
                .ReplaceService<IMigrator, MockMigrator>()
                .Options;

            await using var context = new TestContext(options);

            await context.Database.MigrateIfSupportedAsync(Xunit.TestContext.Current.CancellationToken);
            var migrator = context.GetService<IMigrator>() as MockMigrator;
            Assert.True(migrator?.MigrateAsyncCalled ?? false);
        }

        public class MockMigrator : IMigrator
        {
            public bool MigrateCalled { get; private set; }
            public bool MigrateAsyncCalled { get; private set; }

            public void Migrate(string? targetMigration = null)
            {
                MigrateCalled = true;
            }

            public Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
            {
                MigrateAsyncCalled = true;
                return Task.CompletedTask;
            }

            public string GenerateScript(
                string? fromMigration = null,
                string? toMigration = null,
                MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
            {
                throw new NotImplementedException();
            }

            public bool HasPendingModelChanges() => false;
        }
    }
}
