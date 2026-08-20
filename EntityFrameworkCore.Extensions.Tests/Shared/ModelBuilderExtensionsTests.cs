using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class ModelBuilderExtensionsTests
{
    [Fact]
    public void OverrideDeleteBehaviourChangesEveryConfiguredRelationship()
    {
        using var context = new RelationshipContext(
            new DbContextOptionsBuilder<RelationshipContext>()
                .UseInMemoryDatabase(nameof(OverrideDeleteBehaviourChangesEveryConfiguredRelationship))
                .Options);

        var foreignKeys = context.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()).ToList();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    private sealed class RelationshipContext(DbContextOptions<RelationshipContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Child>()
                .HasOne<Parent>()
                .WithMany()
                .HasForeignKey(child => child.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.OverrideDeleteBehaviour();
        }
    }

    private sealed class Parent
    {
        public int Id { get; set; }
    }

    private sealed class Child
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }
}
