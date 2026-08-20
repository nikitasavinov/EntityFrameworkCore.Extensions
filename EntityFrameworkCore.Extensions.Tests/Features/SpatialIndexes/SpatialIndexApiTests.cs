using Microsoft.EntityFrameworkCore;
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

    private sealed class SpatialEntity
    {
        public int Id { get; set; }
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
    }
}
