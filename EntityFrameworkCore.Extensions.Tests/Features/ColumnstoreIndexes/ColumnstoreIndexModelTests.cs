using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class ColumnstoreIndexModelTests
{
    [Fact]
    public void OnlyDesignTimeRelationalModelContainsColumnstoreAnnotation()
    {
        using var context = new ColumnstoreTestContext();
        var runtimeIndex = Assert.Single(context.Model.GetRelationalModel().FindTable("Sales", "reporting")!.Indexes);
        var designIndex = Assert.Single(context.DesignModel.GetRelationalModel().FindTable("Sales", "reporting")!.Indexes);

        Assert.Null(runtimeIndex.FindAnnotation(AnnotationConstants.ColumnstoreIndex));
        Assert.Equal(true, designIndex[AnnotationConstants.ColumnstoreIndex]);
        Assert.Equal(["TenantId", "Total", "OrderedAt"], designIndex.Columns.Select(column => column.Name));
    }

    [Theory]
    [InlineData("unique", "cannot be unique")]
    [InlineData("descending", "cannot specify sort order")]
    [InlineData("clustered", "SqlServer:Clustered")]
    [InlineData("include", "SqlServer:Include")]
    [InlineData("online", "SqlServer:Online")]
    [InlineData("fill", "SqlServer:FillFactor")]
    [InlineData("sort", "SqlServer:SortInTempDb")]
    [InlineData("compression", "SqlServer:DataCompression")]
    [InlineData("spatial", "both columnstore and spatial")]
    [InlineData("invalid-marker", "must contain a Boolean")]
    [InlineData("second-index", "only one columnstore index")]
    [InlineData("computed", "computed column")]
    [InlineData("sparse", "sparse column")]
    [InlineData("memory", "memory-optimized table")]
    public void InvalidModelConfigurationsFailBeforeSqlGeneration(string option, string expectedMessage)
    {
        using var context = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            var index = entity.HasIndex(sale => new { sale.TenantId, sale.Amount, sale.OrderedAt }, "Analytics");
            switch (option)
            {
                case "unique": index.IsUnique(); break;
                case "descending": index.IsDescending(); break;
                case "clustered": index.IsClustered(); break;
                case "include": index.IncludeProperties(sale => sale.Description); break;
                case "online": index.IsCreatedOnline(); break;
                case "fill": index.HasFillFactor(80); break;
                case "sort": index.SortInTempDb(); break;
                case "compression": index.UseDataCompression(DataCompressionType.Page); break;
                case "spatial": index.HasAnnotation(AnnotationConstants.SpatialIndex, true); break;
                case "invalid-marker": index.HasAnnotation(AnnotationConstants.ColumnstoreIndex, "yes"); break;
                case "second-index": entity.HasColumnstoreIndex(sale => sale.Id); break;
                case "computed": entity.Property(sale => sale.Amount).HasComputedColumnSql("1.0"); break;
                case "sparse":
                    entity.Property<int?>("OptionalValue").IsSparse();
                    entity.Metadata.RemoveIndex(index.Metadata);
                    entity.HasColumnstoreIndex("OptionalValue");
                    break;
                case "memory": entity.ToTable(table => table.IsMemoryOptimized()); break;
            }
        });

        var exception = Assert.Throws<InvalidOperationException>(() => context.DesignModel.GetRelationalModel());
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("varchar(max)")]
    [InlineData("nvarchar(max)")]
    [InlineData("xml")]
    [InlineData("text")]
    [InlineData("ntext")]
    public void UnsupportedStoreTypesAreRejected(string storeType)
    {
        using var context = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.Metadata.RemoveIndex(entity.Metadata.GetIndexes().Single());
            entity.Property(sale => sale.Description).HasColumnType(storeType);
            entity.HasColumnstoreIndex(sale => sale.Description);
        });

        var exception = Assert.Throws<InvalidOperationException>(() => context.DesignModel.GetRelationalModel());
        Assert.Contains("unsupported store type", exception.Message, StringComparison.Ordinal);
        Assert.Contains(storeType, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryAndColumnstoreIndexesOnSameColumnsRemainDistinct()
    {
        using var context = new ColumnstoreTestContext(modelBuilder => modelBuilder.Entity<ColumnstoreSale>()
            .HasIndex(sale => new { sale.TenantId, sale.Amount, sale.OrderedAt })
            .HasDatabaseName("IX_Sales_Lookup"));

        var indexes = context.DesignModel.GetRelationalModel().FindTable("Sales", "reporting")!.Indexes.ToList();
        Assert.Equal(2, indexes.Count);
        Assert.Single(indexes, index => index[AnnotationConstants.ColumnstoreIndex] is true);
        Assert.Single(indexes, index => index.FindAnnotation(AnnotationConstants.ColumnstoreIndex) is null);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void SharedTableIndexRequiresConsistentColumnstoreAnnotations(bool ownedIsColumnstore, bool addOtherColumnstoreIndex)
    {
        using var context = new ColumnstoreTestContext(modelBuilder =>
        {
            modelBuilder.Ignore<ColumnstoreSale>();
            var owner = modelBuilder.Entity<Owner>();
            owner.Property(entity => entity.Amount).HasColumnName("Amount");
            owner.HasColumnstoreIndex(entity => entity.Amount).HasDatabaseName("NCCI_Shared");
            if (addOtherColumnstoreIndex)
            {
                // Visit the valid index first to ensure the table-wide check still reports
                // the mixed mapping, rather than counting it as a second columnstore index.
                owner.HasColumnstoreIndex(entity => entity.Id).HasDatabaseName("A_ValidColumnstore");
            }
            owner.OwnsOne(entity => entity.Details, owned =>
            {
                owned.Property(details => details.Amount).HasColumnName("Amount");
                var index = owned.HasIndex(details => details.Amount).HasDatabaseName("NCCI_Shared");
                if (ownedIsColumnstore)
                {
                    index.HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
                }
            });
            owner.Navigation(entity => entity.Details).IsRequired();
        });

        if (ownedIsColumnstore)
        {
            var index = Assert.Single(context.DesignModel.GetRelationalModel().Tables.Single().Indexes);
            Assert.Equal(true, index[AnnotationConstants.ColumnstoreIndex]);
        }
        else
        {
            var exception = Assert.Throws<InvalidOperationException>(() => context.DesignModel.GetRelationalModel());
            Assert.Contains("combines columnstore and ordinary", exception.Message, StringComparison.Ordinal);
        }
    }

    private sealed class Owner
    {
        public int Id { get; set; }
        public int Amount { get; set; }
        public Details Details { get; set; } = new();
    }

    private sealed class Details
    {
        public int Amount { get; set; }
    }
}
