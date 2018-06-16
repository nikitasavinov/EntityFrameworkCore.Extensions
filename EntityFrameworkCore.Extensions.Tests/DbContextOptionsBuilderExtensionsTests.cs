using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests
{
    public class DbContextOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Throw_on_untranslatable_expression()
        {
            var options = new DbContextOptionsBuilder<TestContext>()
                .UseSqlite("DataSource=:memory:")
                .ThrowOnQueryClientEvaluation()
                .Options;

            var context = new TestContext(options);

            Assert.Throws<InvalidOperationException>(() => context.TestModels.Where(t => SomeFunction(t.Data)).ToList());
        }

        private bool SomeFunction(string input)
        {
            return input.Length > 2 ? input[1] == 'A' : false;
        }
    }

    public class TestContext : DbContext
    {
        public TestContext()
        {
        }

        public TestContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<TestModel> TestModels { get; set; }
    }

    public class TestModel
    {
        public int Id { get; set; }
        public string Data { get; set; }
    }
}
