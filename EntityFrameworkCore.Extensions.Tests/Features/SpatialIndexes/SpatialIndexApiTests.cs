using Microsoft.EntityFrameworkCore;
using EntityFrameworkCore.Extensions.Services;
using NetTopologySuite.Geometries;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class SpatialIndexApiTests
{
    [Fact]
    public void HasSpatialIndexStoresPrimitiveAnnotationsAndReturnsIndexBuilder()
    {
        var modelBuilder = new ModelBuilder();
        var entityBuilder = modelBuilder.Entity<SpatialEntity>();

        var indexBuilder = entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            options => options
                .HasBoundingBox(-10.5, -20.25, 30.75, 40.5)
                .HasCellsPerObject(32));

        Assert.Same(indexBuilder.Metadata, entityBuilder.Metadata.GetIndexes().Single());
        Assert.Equal(true, indexBuilder.Metadata[AnnotationConstants.SpatialIndex]);
        Assert.Equal(-10.5, indexBuilder.Metadata[AnnotationConstants.SpatialIndexBoundingBoxXMin]);
        Assert.Equal(-20.25, indexBuilder.Metadata[AnnotationConstants.SpatialIndexBoundingBoxYMin]);
        Assert.Equal(30.75, indexBuilder.Metadata[AnnotationConstants.SpatialIndexBoundingBoxXMax]);
        Assert.Equal(40.5, indexBuilder.Metadata[AnnotationConstants.SpatialIndexBoundingBoxYMax]);
        Assert.Equal(32, indexBuilder.Metadata[AnnotationConstants.SpatialIndexCellsPerObject]);
    }

    [Fact]
    public void StringHasSpatialIndexConfiguresNamedProperty()
    {
        var modelBuilder = new ModelBuilder();
        var entityBuilder = modelBuilder.Entity<SpatialEntity>();

        var indexBuilder = entityBuilder.HasSpatialIndex(
            nameof(SpatialEntity.Location),
            options => options.HasCellsPerObject(16));

        Assert.Equal(nameof(SpatialEntity.Location), Assert.Single(indexBuilder.Metadata.Properties).Name);
        Assert.Equal(true, indexBuilder.Metadata[AnnotationConstants.SpatialIndex]);
        Assert.Equal(16, indexBuilder.Metadata[AnnotationConstants.SpatialIndexCellsPerObject]);
    }

    [Fact]
    public void NamedSpatialIndexesAllowMultipleIndexesOnOneProperty()
    {
        var modelBuilder = new ModelBuilder();
        var entityBuilder = modelBuilder.Entity<SpatialEntity>();

        var coarseIndex = entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            "CoarseLocationSpatialIndex",
            options => options.HasCellsPerObject(16));
        var fineIndex = entityBuilder.HasSpatialIndex(
            nameof(SpatialEntity.Location),
            "FineLocationSpatialIndex",
            options => options.HasCellsPerObject(64));

        Assert.Equal("CoarseLocationSpatialIndex", coarseIndex.Metadata.Name);
        Assert.Equal(16, coarseIndex.Metadata[AnnotationConstants.SpatialIndexCellsPerObject]);
        Assert.Equal("FineLocationSpatialIndex", fineIndex.Metadata.Name);
        Assert.Equal(64, fineIndex.Metadata[AnnotationConstants.SpatialIndexCellsPerObject]);
        Assert.Equal(2, entityBuilder.Metadata.GetIndexes().Count());
    }

    [Fact]
    public void OwnedNavigationHasSpatialIndexSupportsExpressionAndStringOverloads()
    {
        var modelBuilder = new ModelBuilder();
        var ownedNavigationBuilder = modelBuilder.Entity<SpatialOwner>()
            .OwnsOne(owner => owner.Details);

        var expressionIndex = ownedNavigationBuilder.HasSpatialIndex(
            details => details.Location,
            "OwnedExpressionSpatialIndex",
            options => options.HasCellsPerObject(16));
        var stringIndex = ownedNavigationBuilder.HasSpatialIndex(
            nameof(OwnedSpatialEntity.Location),
            "OwnedStringSpatialIndex",
            options => options.HasCellsPerObject(64));

        Assert.Equal("OwnedExpressionSpatialIndex", expressionIndex.Metadata.Name);
        Assert.Equal(16, expressionIndex.Metadata[AnnotationConstants.SpatialIndexCellsPerObject]);
        Assert.Equal("OwnedStringSpatialIndex", stringIndex.Metadata.Name);
        Assert.Equal(64, stringIndex.Metadata[AnnotationConstants.SpatialIndexCellsPerObject]);
    }

    [Fact]
    public void SpatialIndexOptionsRejectInvalidValues()
    {
        var modelBuilder = new ModelBuilder();
        var entityBuilder = modelBuilder.Entity<SpatialEntity>();

        Assert.Throws<ArgumentOutOfRangeException>(() => entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            options => options.HasBoundingBox(double.NaN, -10, 10, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            options => options.HasBoundingBox(10, -10, 10, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            options => options.HasBoundingBox(-10, 10, 10, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            options => options.HasCellsPerObject(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => entityBuilder.HasSpatialIndex(
            entity => entity.Location,
            options => options.HasCellsPerObject(8193)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SpatialIndexAnnotationsRejectPartialBoundingBox(int coordinateCount)
    {
        var modelBuilder = new ModelBuilder();
        var indexBuilder = modelBuilder.Entity<SpatialEntity>()
            .HasIndex(entity => entity.Location)
            .HasAnnotation(AnnotationConstants.SpatialIndex, true);
        var coordinateNames = new[]
        {
            AnnotationConstants.SpatialIndexBoundingBoxXMin,
            AnnotationConstants.SpatialIndexBoundingBoxYMin,
            AnnotationConstants.SpatialIndexBoundingBoxXMax,
            AnnotationConstants.SpatialIndexBoundingBoxYMax,
        };

        for (var index = 0; index < coordinateCount; index++)
        {
            indexBuilder.HasAnnotation(coordinateNames[index], (double)index);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => SpatialIndexAnnotation.GetOptions(indexBuilder.Metadata, "Places.SIX_Location"));

        Assert.Contains("all four bounding-box coordinates", exception.Message, StringComparison.Ordinal);
    }

    private sealed class SpatialEntity
    {
        public int Id { get; set; }
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
    }

    private sealed class SpatialOwner
    {
        public int Id { get; set; }
        public OwnedSpatialEntity Details { get; set; } = new();
    }

    private sealed class OwnedSpatialEntity
    {
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
    }
}
