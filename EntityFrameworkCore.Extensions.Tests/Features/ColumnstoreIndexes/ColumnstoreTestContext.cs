using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Extensions.Tests;

internal sealed class ColumnstoreTestContext : DbContext
{
    public ColumnstoreTestContext(Action<ModelBuilder>? configure = null, string? connectionString = null)
        : base(new DbContextOptionsBuilder<ColumnstoreTestContext>()
            .UseSqlServer(connectionString ?? "Server=(localdb)\\mssqllocaldb;Database=NotUsed")
            .UseEntityFrameworkCoreExtensions()
            .ReplaceService<IModelCacheKeyFactory, ModelCacheKeyFactory>()
            .Options)
    {
        Configure = configure;
    }

    private Action<ModelBuilder>? Configure { get; }

    public IModel DesignModel => this.GetService<IDesignTimeModel>().Model;

    public static IndexBuilder<ColumnstoreSale> AnalyticsIndex(EntityTypeBuilder<ColumnstoreSale> entity)
        => entity.HasIndex(sale => new { sale.TenantId, sale.Amount, sale.OrderedAt }, "Analytics");

    public static IReadOnlyList<MigrationOperation> Differences(ColumnstoreTestContext source, ColumnstoreTestContext target)
        => target.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source.DesignModel.GetRelationalModel(), target.DesignModel.GetRelationalModel());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var sale = modelBuilder.Entity<ColumnstoreSale>();
        sale.ToTable("Sales", "reporting");
        sale.Property(entity => entity.Amount).HasColumnName("Total").HasPrecision(18, 2);
        sale.Property(entity => entity.Description).HasMaxLength(200);
        sale.HasColumnstoreIndex(entity => new { entity.TenantId, entity.Amount, entity.OrderedAt }, "Analytics")
            .HasDatabaseName("NCCI_Sales_Analytics");
        Configure?.Invoke(modelBuilder);
    }

    private sealed class ModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((ColumnstoreTestContext)context).Configure, designTime);
    }
}

internal sealed class ColumnstoreSale
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OrderedAt { get; set; }
    public string Description { get; set; } = string.Empty;
}
