using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class ColumnstoreIndexApiTests
{
    [Fact]
    public void CompositeColumnstoreIndexSupportsFilterAndDatabaseName()
    {
        var entity = new ModelBuilder().Entity<ColumnstoreSale>();
        var index = entity.HasColumnstoreIndex(sale => new { sale.TenantId, sale.Amount })
            .HasDatabaseName("NCCI_Sales")
            .HasFilter("[TenantId] > 0");

        Assert.Equal(["TenantId", "Amount"], index.Metadata.Properties.Select(property => property.Name));
        Assert.Equal(true, index.Metadata[AnnotationConstants.ColumnstoreIndex]);
        Assert.Equal("NCCI_Sales", index.Metadata.GetDatabaseName());
        Assert.Equal("[TenantId] > 0", index.Metadata.GetFilter());
    }

    [Fact]
    public void NamedColumnstoreIndexCanCoexistWithOrdinaryIndexOnSameProperties()
    {
        var entity = new ModelBuilder().Entity<ColumnstoreSale>();
        var ordinary = entity.HasIndex(sale => sale.TenantId);
        var columnstore = entity.HasColumnstoreIndex(sale => sale.TenantId, "Analytics");

        Assert.Equal(2, entity.Metadata.GetIndexes().Count());
        Assert.Null(ordinary.Metadata.FindAnnotation(AnnotationConstants.ColumnstoreIndex));
        Assert.Equal(true, columnstore.Metadata[AnnotationConstants.ColumnstoreIndex]);
    }

    [Fact]
    public void StringOverloadsConfigureSingleCompositeAndNamedIndexes()
    {
        EntityTypeBuilder entity = new ModelBuilder().Entity<ColumnstoreSale>();
        var single = entity.HasColumnstoreIndex("TenantId");
        var composite = entity.HasColumnstoreIndex("TenantId", "Amount");
        var named = entity.HasColumnstoreIndex(["TenantId", "Amount"], "Analytics");

        Assert.Single(single.Metadata.Properties);
        Assert.Equal(2, composite.Metadata.Properties.Count);
        Assert.Equal("Analytics", named.Metadata.Name);
        Assert.Equal(true, named.Metadata[AnnotationConstants.ColumnstoreIndex]);
    }

    [Fact]
    public void OwnedNavigationOverloadsPreserveSelectedPropertiesAndNames()
    {
        var owned = new ModelBuilder().Entity<Owner>().OwnsOne(owner => owner.Details);
        var expression = owned.HasColumnstoreIndex(details => details.Amount);
        var namedExpression = owned.HasColumnstoreIndex(details => details.Amount, "Expression");
        var strings = owned.HasColumnstoreIndex("Amount");
        var namedStrings = owned.HasColumnstoreIndex(["Amount"], "Strings");

        Assert.Same(expression.Metadata, strings.Metadata);
        Assert.Equal("Expression", namedExpression.Metadata.Name);
        Assert.Equal("Strings", namedStrings.Metadata.Name);
        Assert.Equal(true, namedStrings.Metadata[AnnotationConstants.ColumnstoreIndex]);
    }

    [Fact]
    public void InvalidArgumentsAreRejected()
    {
        var entity = new ModelBuilder().Entity<ColumnstoreSale>();
        Assert.Throws<ArgumentNullException>(() =>
            ((EntityTypeBuilder<ColumnstoreSale>)null!).HasColumnstoreIndex(sale => sale.Amount));
        Assert.Throws<ArgumentNullException>(() =>
            entity.HasColumnstoreIndex((Expression<Func<ColumnstoreSale, object?>>)null!));
        Assert.Throws<ArgumentException>(() => entity.HasColumnstoreIndex(sale => sale.Amount, " "));
        Assert.Throws<ArgumentException>(() => entity.HasColumnstoreIndex(Array.Empty<string>()));
    }

    private sealed class Owner
    {
        public int Id { get; set; }
        public Details Details { get; set; } = new();
    }

    private sealed class Details
    {
        public decimal Amount { get; set; }
    }
}
