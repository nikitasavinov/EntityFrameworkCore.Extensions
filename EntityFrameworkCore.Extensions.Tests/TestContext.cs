using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Extensions.Tests
{
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
