using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NetTopologySuite.Geometries;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class SpatialIndexSqlGeneratorTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=NotUsed";

    [Fact]
    public void GeographyModelPropagatesAnnotationsAndGeneratesSpatialSql()
    {
        using var context = CreateGeographyContext();
        var model = GetDesignModel(context);
        var operations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source: null, model.GetRelationalModel());

        var createIndex = Assert.Single(operations.OfType<CreateIndexOperation>());
        Assert.Equal(true, createIndex[AnnotationConstants.SpatialIndex]);
        Assert.Equal("geography", createIndex[AnnotationConstants.SpatialIndexType]);
        Assert.Equal(32, createIndex[AnnotationConstants.SpatialIndexCellsPerObject]);

        var command = Assert.Single(
            GenerateCommands(context, operations, model),
            candidate => candidate.CommandText.Contains("CREATE SPATIAL INDEX", StringComparison.Ordinal));
        Assert.Equal(
            """
            CREATE SPATIAL INDEX [SIX_Places_Location] ON [odd]]schema].[Places] ([Location]]])
            USING GEOGRAPHY_AUTO_GRID
            WITH (CELLS_PER_OBJECT = 32);
            """,
            command.CommandText.Trim());
    }

    [Fact]
    public void GeometryModelGeneratesAutoGridSqlWithInvariantBoundingBox()
    {
        using var context = CreateGeometryContext();
        var model = GetDesignModel(context);
        var operations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source: null, model.GetRelationalModel());

        var createIndex = Assert.Single(operations.OfType<CreateIndexOperation>());
        Assert.Equal("geometry", createIndex[AnnotationConstants.SpatialIndexType]);

        var command = Assert.Single(
            GenerateCommands(context, operations, model),
            candidate => candidate.CommandText.Contains("CREATE SPATIAL INDEX", StringComparison.Ordinal));
        Assert.Equal(
            """
            CREATE SPATIAL INDEX [SIX_Places_Location] ON [odd]]schema].[Places] ([Location]]])
            USING GEOMETRY_AUTO_GRID
            WITH (BOUNDING_BOX = (-180.5, -90.25, 180.5, 90.25), CELLS_PER_OBJECT = 64);
            """,
            command.CommandText.Trim());
    }

    [Fact]
    public void ChangingSpatialOptionsDropsAndRecreatesIndex()
    {
        using var sourceContext = CreateGeographyContext();
        using var targetContext = CreateChangedGeographyContext();
        var sourceModel = GetDesignModel(sourceContext);
        var targetModel = GetDesignModel(targetContext);

        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        Assert.Collection(
            operations,
            operation => Assert.IsType<DropIndexOperation>(operation),
            operation =>
            {
                var createIndex = Assert.IsType<CreateIndexOperation>(operation);
                Assert.Equal(128, createIndex[AnnotationConstants.SpatialIndexCellsPerObject]);
            });

        var commands = GenerateCommands(targetContext, operations, targetModel);
        Assert.Contains("DROP INDEX [SIX_Places_Location] ON [odd]]schema].[Places];", commands[0].CommandText);
        Assert.Contains("CELLS_PER_OBJECT = 128", commands[1].CommandText);
    }

    [Fact]
    public void RemovingSpatialIndexUsesEfCoreDropIndexOperation()
    {
        using var sourceContext = CreateGeographyContext();
        using var targetContext = CreateNoIndexContext();
        var sourceModel = GetDesignModel(sourceContext);
        var targetModel = GetDesignModel(targetContext);

        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var dropIndex = Assert.Single(operations);
        Assert.IsType<DropIndexOperation>(dropIndex);
        var command = Assert.Single(GenerateCommands(targetContext, operations, targetModel));
        Assert.Equal(
            "DROP INDEX [SIX_Places_Location] ON [odd]]schema].[Places];",
            command.CommandText.Trim());
    }

    [Fact]
    public void RenamingSpatialIndexUsesEfCoreRenameIndexOperation()
    {
        using var sourceContext = CreateGeographyContext();
        using var targetContext = CreateRenamedGeographyContext();
        var sourceModel = GetDesignModel(sourceContext);
        var targetModel = GetDesignModel(targetContext);

        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var renameIndex = Assert.IsType<RenameIndexOperation>(Assert.Single(operations));
        Assert.Equal("SIX_Places_Location", renameIndex.Name);
        Assert.Equal("SIX_Places_Location_Renamed", renameIndex.NewName);
        var command = Assert.Single(GenerateCommands(targetContext, operations, targetModel));
        Assert.Contains("sp_rename", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("SIX_Places_Location_Renamed", command.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void RawSpatialIndexOperationRejectsEmptyDescendingArray()
    {
        using var context = CreateGeographyContext();
        var operation = CreateSpatialIndexOperation();
        operation.IsDescending = [];

        var exception = Assert.Throws<InvalidOperationException>(
            () => GenerateCommands(context, [operation], model: null));

        Assert.Contains("cannot specify sort order", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawSpatialIndexOperationRejectsOnlineAnnotation()
    {
        using var context = CreateGeographyContext();
        var operation = CreateSpatialIndexOperation();
        operation.AddAnnotation("SqlServer:Online", true);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GenerateCommands(context, [operation], model: null));

        Assert.Contains("does not support additional SQL Server index options yet", exception.Message, StringComparison.Ordinal);
    }

    private static GeographyContext CreateGeographyContext()
    {
        var options = new DbContextOptionsBuilder<GeographyContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new GeographyContext(options);
    }

    private static ChangedGeographyContext CreateChangedGeographyContext()
    {
        var options = new DbContextOptionsBuilder<ChangedGeographyContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new ChangedGeographyContext(options);
    }

    private static GeometryContext CreateGeometryContext()
    {
        var options = new DbContextOptionsBuilder<GeometryContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new GeometryContext(options);
    }

    private static RenamedGeographyContext CreateRenamedGeographyContext()
    {
        var options = new DbContextOptionsBuilder<RenamedGeographyContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new RenamedGeographyContext(options);
    }

    private static NoIndexContext CreateNoIndexContext()
    {
        var options = new DbContextOptionsBuilder<NoIndexContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new NoIndexContext(options);
    }

    private static IModel GetDesignModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    private static IReadOnlyList<MigrationCommand> GenerateCommands(
        DbContext context,
        IReadOnlyList<MigrationOperation> operations,
        IModel? model)
        => context.GetService<IMigrationsSqlGenerator>().Generate(operations, model);

    private static CreateIndexOperation CreateSpatialIndexOperation()
    {
        var operation = new CreateIndexOperation
        {
            Name = "SIX_Places_Location",
            Table = "Places",
            Columns = ["Location"]
        };
        operation.AddAnnotation(AnnotationConstants.SpatialIndex, true);
        operation.AddAnnotation(AnnotationConstants.SpatialIndexType, "geography");
        return operation;
    }

    private static void ConfigureSpatialModel(
        ModelBuilder modelBuilder,
        string storeType,
        int? cellsPerObject,
        bool includeIndex = true,
        bool includeBoundingBox = false)
    {
        var entityBuilder = modelBuilder.Entity<SpatialEntity>();
        entityBuilder.ToTable("Places", "odd]schema");
        entityBuilder.HasKey(entity => entity.Id);
        entityBuilder.Property(entity => entity.Location)
            .HasColumnName("Location]")
            .HasColumnType(storeType);

        if (!includeIndex)
        {
            return;
        }

        entityBuilder.HasSpatialIndex(
                entity => entity.Location,
                options =>
                {
                    if (includeBoundingBox)
                    {
                        options.HasBoundingBox(-180.5, -90.25, 180.5, 90.25);
                    }

                    if (cellsPerObject is not null)
                    {
                        options.HasCellsPerObject(cellsPerObject.Value);
                    }
                })
            .HasDatabaseName("SIX_Places_Location");
    }

    private sealed class GeographyContext(DbContextOptions<GeographyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geography", 32);
    }

    private sealed class ChangedGeographyContext(DbContextOptions<ChangedGeographyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geography", 128);
    }

    private sealed class GeometryContext(DbContextOptions<GeometryContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geometry", 64, includeBoundingBox: true);
    }

    private sealed class RenamedGeographyContext(DbContextOptions<RenamedGeographyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureSpatialModel(modelBuilder, "geography", 32);
            modelBuilder.Entity<SpatialEntity>()
                .HasIndex(entity => entity.Location)
                .HasDatabaseName("SIX_Places_Location_Renamed");
        }
    }

    private sealed class NoIndexContext(DbContextOptions<NoIndexContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, "geography", cellsPerObject: null, includeIndex: false);
    }

    private sealed class SpatialEntity
    {
        public int Id { get; set; }
        public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
    }
}
