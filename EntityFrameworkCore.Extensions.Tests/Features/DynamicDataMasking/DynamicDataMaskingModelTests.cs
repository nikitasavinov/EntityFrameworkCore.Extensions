using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class DynamicDataMaskingModelTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=NotUsed";

    [Fact]
    public void RuntimeModelDoesNotContainMigrationMaskingAnnotation()
    {
        using var context = CreateMaskedContext();

        var runtimeColumn = context.Model.GetRelationalModel()
            .FindTable("Order", "odd]schema")!
            .FindColumn("Select]")!;
        var designColumn = context.GetService<IDesignTimeModel>().Model.GetRelationalModel()
            .FindTable("Order", "odd]schema")!
            .FindColumn("Select]")!;

        Assert.Null(runtimeColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking));
        Assert.Equal(
            MaskingFunctions.Default(),
            designColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
    }

    [Fact]
    public void ConflictingMasksOnSharedColumnAreRejected()
    {
        var options = new DbContextOptionsBuilder<ConflictingSharedColumnContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        using var context = new ConflictingSharedColumnContext(options);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _ = context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Contains("conflicting dynamic data masks", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SharedSecrets.Secret", exception.Message, StringComparison.Ordinal);
    }

    private static MaskedContext CreateMaskedContext()
    {
        var options = new DbContextOptionsBuilder<MaskedContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new MaskedContext(options);
    }

    private sealed class MaskedContext(DbContextOptions<MaskedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entityBuilder = modelBuilder.Entity<SecretEntity>();
            entityBuilder.ToTable("Order", "odd]schema");
            entityBuilder.HasKey(entity => entity.Id);
            entityBuilder.Property(entity => entity.Secret)
                .HasColumnName("Select]")
                .HasDataMask(MaskingFunctions.Default());
        }
    }

    private sealed class ConflictingSharedColumnContext(DbContextOptions<ConflictingSharedColumnContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedColumnPrincipal>(entityBuilder =>
            {
                entityBuilder.ToTable("SharedSecrets");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.Property(entity => entity.Secret)
                    .HasColumnName("Secret")
                    .HasDataMask(MaskingFunctions.Default());
                entityBuilder.HasOne(entity => entity.Details)
                    .WithOne()
                    .HasForeignKey<SharedColumnDetails>(entity => entity.Id);
            });

            modelBuilder.Entity<SharedColumnDetails>(entityBuilder =>
            {
                entityBuilder.ToTable("SharedSecrets");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.Property(entity => entity.Secret)
                    .HasColumnName("Secret")
                    .HasDataMask(MaskingFunctions.Email());
            });
        }
    }

    private sealed class SecretEntity
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class SharedColumnPrincipal
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
        public SharedColumnDetails Details { get; set; } = null!;
    }

    private sealed class SharedColumnDetails
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }
}
