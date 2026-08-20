using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NetTopologySuite.Geometries;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class SpatialIndexModelTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=NotUsed";

    [Fact]
    public void RuntimeModelDoesNotContainSpatialMigrationAnnotations()
    {
        using var context = new GeographyContext(CreateOptions<GeographyContext>());

        var runtimeIndex = context.Model.GetRelationalModel()
            .FindTable("Places", "odd]schema")!
            .Indexes.Single();
        var designIndex = GetDesignModel(context).GetRelationalModel()
            .FindTable("Places", "odd]schema")!
            .Indexes.Single();

        Assert.Null(runtimeIndex.FindAnnotation(AnnotationConstants.SpatialIndex));
        Assert.Equal(true, designIndex[AnnotationConstants.SpatialIndex]);
        Assert.Equal("geography", designIndex[AnnotationConstants.SpatialIndexType]);
    }

    [Fact]
    public void GeometrySpatialIndexRequiresBoundingBox()
    {
        using var context = new GeometryWithoutBoundingBoxContext(CreateOptions<GeometryWithoutBoundingBoxContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("requires a bounding box", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GeographySpatialIndexRejectsBoundingBox()
    {
        using var context = new GeographyWithBoundingBoxContext(CreateOptions<GeographyWithBoundingBoxContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("cannot have a bounding box", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsUnsupportedIndexShapes()
    {
        using var context = new InvalidIndexShapeContext(CreateOptions<InvalidIndexShapeContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("exactly one column", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRequiresClusteredPrimaryKey()
    {
        using var context = new NonClusteredPrimaryKeyContext(CreateOptions<NonClusteredPrimaryKeyContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("clustered primary key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsUnsupportedSqlServerIndexOptions()
    {
        using var context = new UnsupportedIndexOptionsContext(CreateOptions<UnsupportedIndexOptionsContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("does not support additional SQL Server index options yet", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsDescendingSortOrder()
    {
        using var context = new DescendingSpatialIndexContext(CreateOptions<DescendingSpatialIndexContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("cannot specify sort order", exception.Message, StringComparison.Ordinal);
    }

    private static DbContextOptions<TContext> CreateOptions<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;

    private static IModel GetDesignModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    private static void ConfigureSpatialModel(
        ModelBuilder modelBuilder,
        string storeType,
        bool includeBoundingBox = false)
    {
        var entityBuilder = modelBuilder.Entity<SpatialEntity>();
        entityBuilder.ToTable("Places", "odd]schema");
        entityBuilder.HasKey(entity => entity.Id);
        entityBuilder.Property(entity => entity.Location)
            .HasColumnName("Location]")
            .HasColumnType(storeType);
        entityBuilder.HasSpatialIndex(
                entity => entity.Location,
                options =>
                {
                    if (includeBoundingBox)
                    {
                        options.HasBoundingBox(-180.5, -90.25, 180.5, 90.25);
                    }

                    options.HasCellsPerObject(32);
                })
            .HasDatabaseName("SIX_Places_Location");
    }

    private sealed class GeographyContext(DbContextOptions<GeographyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geography");
    }

    private sealed class GeometryWithoutBoundingBoxContext(DbContextOptions<GeometryWithoutBoundingBoxContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geometry");
    }

    private sealed class GeographyWithBoundingBoxContext(DbContextOptions<GeographyWithBoundingBoxContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geography", includeBoundingBox: true);
    }

    private sealed class InvalidIndexShapeContext(DbContextOptions<InvalidIndexShapeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entityBuilder = modelBuilder.Entity<SpatialEntity>();
            entityBuilder.ToTable("Places");
            entityBuilder.HasKey(entity => entity.Id);
            entityBuilder.Property(entity => entity.Location).HasColumnType("geography");
            entityBuilder.HasIndex(entity => new { entity.Location, entity.Id })
                .HasAnnotation(AnnotationConstants.SpatialIndex, true);
        }
    }

    private sealed class NonClusteredPrimaryKeyContext(DbContextOptions<NonClusteredPrimaryKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entityBuilder = modelBuilder.Entity<SpatialEntity>();
            entityBuilder.ToTable("Places");
            entityBuilder.HasKey(entity => entity.Id).IsClustered(false);
            entityBuilder.Property(entity => entity.Location).HasColumnType("geography");
            entityBuilder.HasSpatialIndex(entity => entity.Location);
        }
    }

    private sealed class UnsupportedIndexOptionsContext(DbContextOptions<UnsupportedIndexOptionsContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>()
                .HasIndex(entity => entity.Location)
                .HasFillFactor(80);
        }
    }

    private sealed class DescendingSpatialIndexContext(DbContextOptions<DescendingSpatialIndexContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>()
                .HasIndex(entity => entity.Location)
                .IsDescending();
        }
    }

    private sealed class SpatialEntity
    {
        public int Id { get; set; }
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
    }
}
