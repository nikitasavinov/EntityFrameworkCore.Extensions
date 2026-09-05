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
    public void NamedSpatialIndexesOnSamePropertyRemainDistinct()
    {
        using var context = new NamedSpatialIndexesContext(CreateOptions<NamedSpatialIndexesContext>());

        var indexes = GetDesignModel(context).GetRelationalModel()
            .FindTable("Places", schema: null)!
            .Indexes
            .OrderBy(index => index.Name)
            .ToList();

        Assert.Collection(
            indexes,
            index =>
            {
                Assert.Equal("SIX_Places_Location_Coarse", index.Name);
                Assert.Equal(16, index[AnnotationConstants.SpatialIndexCellsPerObject]);
            },
            index =>
            {
                Assert.Equal("SIX_Places_Location_Fine", index.Name);
                Assert.Equal(64, index[AnnotationConstants.SpatialIndexCellsPerObject]);
            });
    }

    [Fact]
    public void OwnedNavigationSpatialIndexBuildsDesignModel()
    {
        using var context = new OwnedSpatialIndexContext(CreateOptions<OwnedSpatialIndexContext>());

        var index = Assert.Single(
            GetDesignModel(context).GetRelationalModel().FindTable("OwnedLocations", schema: null)!.Indexes);

        Assert.Equal("SIX_OwnedLocations_Location", index.Name);
        Assert.Equal(true, index[AnnotationConstants.SpatialIndex]);
        Assert.Equal("geography", index[AnnotationConstants.SpatialIndexType]);
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
    public void SpatialIndexRequiresPrimaryKey()
    {
        using var context = new MissingPrimaryKeyContext(CreateOptions<MissingPrimaryKeyContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("must have a primary key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsUniqueIndex()
    {
        using var context = new UniqueSpatialIndexContext(CreateOptions<UniqueSpatialIndexContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("cannot be unique", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsFilteredIndex()
    {
        using var context = new FilteredSpatialIndexContext(CreateOptions<FilteredSpatialIndexContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("cannot have a filter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsClusteredIndex()
    {
        using var context = new ClusteredSpatialIndexContext(CreateOptions<ClusteredSpatialIndexContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("cannot be clustered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsIncludedColumns()
    {
        using var context = new IncludedColumnSpatialIndexContext(CreateOptions<IncludedColumnSpatialIndexContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("cannot have included columns", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsOnlineOption()
    {
        using var context = new OnlineSpatialIndexContext(CreateOptions<OnlineSpatialIndexContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("does not support ONLINE", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsNonSpatialStoreType()
    {
        using var context = new NonSpatialStoreTypeContext(CreateOptions<NonSpatialStoreTypeContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("Only SQL Server geography and geometry columns are supported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpatialIndexRejectsConflictingOptionsOnMappedIndexes()
    {
        using var context = new ConflictingMappedIndexesContext(CreateOptions<ConflictingMappedIndexesContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => GetDesignModel(context).GetRelationalModel());

        Assert.Contains("conflicting spatial index options", exception.Message, StringComparison.Ordinal);
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

    private sealed class NamedSpatialIndexesContext(DbContextOptions<NamedSpatialIndexesContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entityBuilder = modelBuilder.Entity<SpatialEntity>();
            entityBuilder.ToTable("Places");
            entityBuilder.HasKey(entity => entity.Id);
            entityBuilder.Property(entity => entity.Location).HasColumnType("geography");
            entityBuilder.HasSpatialIndex(
                    entity => entity.Location,
                    "CoarseLocationSpatialIndex",
                    options => options.HasCellsPerObject(16))
                .HasDatabaseName("SIX_Places_Location_Coarse");
            entityBuilder.HasSpatialIndex(
                    entity => entity.Location,
                    "FineLocationSpatialIndex",
                    options => options.HasCellsPerObject(64))
                .HasDatabaseName("SIX_Places_Location_Fine");
        }
    }

    private sealed class OwnedSpatialIndexContext(DbContextOptions<OwnedSpatialIndexContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SpatialOwner>(entityBuilder =>
            {
                entityBuilder.ToTable("SpatialOwners");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.OwnsOne(
                    entity => entity.Details,
                    ownedNavigationBuilder =>
                    {
                        ownedNavigationBuilder.ToTable("OwnedLocations");
                        ownedNavigationBuilder.Property(details => details.Location).HasColumnType("geography");
                        ownedNavigationBuilder.HasSpatialIndex(details => details.Location)
                            .HasDatabaseName("SIX_OwnedLocations_Location");
                    });
            });
        }
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

    private sealed class MissingPrimaryKeyContext(DbContextOptions<MissingPrimaryKeyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entityBuilder = modelBuilder.Entity<SpatialEntity>();
            entityBuilder.ToTable("Places");
            entityBuilder.HasNoKey();
            entityBuilder.Property(entity => entity.Location).HasColumnType("geography");
            entityBuilder.HasSpatialIndex(entity => entity.Location);
        }
    }

    private sealed class UniqueSpatialIndexContext(DbContextOptions<UniqueSpatialIndexContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>().HasIndex(entity => entity.Location).IsUnique();
        }
    }

    private sealed class FilteredSpatialIndexContext(DbContextOptions<FilteredSpatialIndexContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>().HasIndex(entity => entity.Location).HasFilter("[Id] > 0");
        }
    }

    private sealed class ClusteredSpatialIndexContext(DbContextOptions<ClusteredSpatialIndexContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>().HasIndex(entity => entity.Location).IsClustered();
        }
    }

    private sealed class IncludedColumnSpatialIndexContext(DbContextOptions<IncludedColumnSpatialIndexContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>()
                .HasIndex(entity => entity.Location)
                .IncludeProperties(entity => entity.Name);
        }
    }

    private sealed class OnlineSpatialIndexContext(DbContextOptions<OnlineSpatialIndexContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography");
            modelBuilder.Entity<SpatialEntity>().HasIndex(entity => entity.Location).IsCreatedOnline();
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

    private sealed class NonSpatialStoreTypeContext(DbContextOptions<NonSpatialStoreTypeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entityBuilder = modelBuilder.Entity<NonSpatialEntity>();
            entityBuilder.ToTable("Places");
            entityBuilder.HasKey(entity => entity.Id);
            entityBuilder.Property(entity => entity.Location).HasColumnType("nvarchar(100)");
            entityBuilder.HasSpatialIndex(entity => entity.Location);
        }
    }

    private sealed class ConflictingMappedIndexesContext(DbContextOptions<ConflictingMappedIndexesContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedSpatialPrincipal>(entityBuilder =>
            {
                entityBuilder.ToTable("SharedPlaces");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.Property(entity => entity.Location)
                    .HasColumnName("Location")
                    .HasColumnType("geography");
                entityBuilder.HasSpatialIndex(
                        entity => entity.Location,
                        options => options.HasCellsPerObject(16))
                    .HasDatabaseName("SIX_SharedPlaces_Location");
                entityBuilder.HasOne(entity => entity.Details)
                    .WithOne()
                    .HasForeignKey<SharedSpatialDetails>(entity => entity.Id);
            });

            modelBuilder.Entity<SharedSpatialDetails>(entityBuilder =>
            {
                entityBuilder.ToTable("SharedPlaces");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.Property(entity => entity.Location)
                    .HasColumnName("Location")
                    .HasColumnType("geography");
                entityBuilder.HasSpatialIndex(
                        entity => entity.Location,
                        options => options.HasCellsPerObject(32))
                    .HasDatabaseName("SIX_SharedPlaces_Location");
            });
        }
    }

    private sealed class SpatialEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
    }

    private sealed class NonSpatialEntity
    {
        public int Id { get; set; }
        public string Location { get; set; } = string.Empty;
    }

    private sealed class SharedSpatialPrincipal
    {
        public int Id { get; set; }
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
        public SharedSpatialDetails Details { get; set; } = null!;
    }

    private sealed class SharedSpatialDetails
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
