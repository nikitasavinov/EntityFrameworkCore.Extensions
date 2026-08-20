using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NetTopologySuite.Geometries;
using static EntityFrameworkCore.Extensions.Tests.SqlServerIntegrationTestDatabase;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class SpatialIndexSqlServerIntegrationTests
{
    public static bool HasSqlServerConnectionString
        => IsConfigured;

    [Fact(
        Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.",
        SkipUnless = nameof(HasSqlServerConnectionString),
        Timeout = 120_000)]
    public async Task SpatialIndexesCreateChangeAndDropOnSqlServer()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var databaseName = $"EfCoreExtensions_{Guid.NewGuid():N}";
        var connectionString = await CreateDatabaseConnectionStringAsync(databaseName, cancellationToken);
        await using var spatialContext = CreateSpatialContext(connectionString);
        await using var changedSpatialContext = CreateChangedSpatialContext(connectionString);
        await using var noSpatialIndexesContext = CreateNoSpatialIndexesContext(connectionString);

        try
        {
            Assert.True(await spatialContext.Database.EnsureCreatedAsync(cancellationToken));
            Assert.Equal(2, await GetSpatialIndexCountAsync(spatialContext, cancellationToken));
            Assert.Equal(
                "GEOGRAPHY_AUTO_GRID",
                await GetSpatialIndexTessellationAsync(
                    spatialContext,
                    "SIX_SpatialPlaces_GeographyLocation",
                    cancellationToken));
            Assert.Equal(
                "GEOMETRY_AUTO_GRID",
                await GetSpatialIndexTessellationAsync(
                    spatialContext,
                    "SIX_SpatialPlaces_GeometryLocation",
                    cancellationToken));
            Assert.Equal(
                32,
                await GetSpatialIndexCellsPerObjectAsync(
                    spatialContext,
                    "SIX_SpatialPlaces_GeographyLocation",
                    cancellationToken));
            Assert.Equal(
                -1000d,
                await GetSpatialIndexBoundingBoxXMinAsync(
                    spatialContext,
                    "SIX_SpatialPlaces_GeometryLocation",
                    cancellationToken));

            var connection = spatialContext.Database.GetDbConnection();
            await ExecuteNonQueryAsync(
                connection,
                """
                INSERT INTO [dbo].[SpatialPlaces] ([GeographyLocation], [GeometryLocation])
                VALUES (
                    geography::Point(52.3702, 4.8952, 4326),
                    geometry::Point(0, 0, 0));
                """,
                cancellationToken);

            const string indexedSpatialQuery = """
                SELECT TOP (1) [place].[Id]
                FROM [dbo].[SpatialPlaces] AS [place]
                    WITH (INDEX([SIX_SpatialPlaces_GeographyLocation]))
                WHERE [place].[GeographyLocation].STDistance(
                    geography::Point(52.3702, 4.8952, 4326)) <= 1000;
                """;
            Assert.Equal(
                1,
                Convert.ToInt32(
                    await ExecuteScalarAsync(connection, indexedSpatialQuery, cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture));

            var showplan = await GetEstimatedExecutionPlanAsync(
                connection,
                indexedSpatialQuery,
                cancellationToken);
            Assert.Contains(
                "Index=\"[SIX_SpatialPlaces_GeographyLocation]\"",
                showplan,
                StringComparison.Ordinal);
            Assert.Contains("IndexKind=\"Spatial\"", showplan, StringComparison.Ordinal);

            var spatialModel = spatialContext.GetService<IDesignTimeModel>().Model;
            var changedSpatialModel = changedSpatialContext.GetService<IDesignTimeModel>().Model;
            var changeOperations = changedSpatialContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                spatialModel.GetRelationalModel(),
                changedSpatialModel.GetRelationalModel());
            Assert.Single(changeOperations.OfType<DropIndexOperation>());
            Assert.Single(changeOperations.OfType<CreateIndexOperation>());
            await ExecuteMigrationOperationsAsync(
                changedSpatialContext,
                changeOperations,
                changedSpatialModel,
                cancellationToken);
            Assert.Equal(
                128,
                await GetSpatialIndexCellsPerObjectAsync(
                    changedSpatialContext,
                    "SIX_SpatialPlaces_GeographyLocation",
                    cancellationToken));

            var noSpatialIndexesModel = noSpatialIndexesContext.GetService<IDesignTimeModel>().Model;
            var removeOperations = noSpatialIndexesContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                changedSpatialModel.GetRelationalModel(),
                noSpatialIndexesModel.GetRelationalModel());
            Assert.Equal(2, removeOperations.OfType<DropIndexOperation>().Count());
            await ExecuteMigrationOperationsAsync(
                noSpatialIndexesContext,
                removeOperations,
                noSpatialIndexesModel,
                cancellationToken);
            Assert.Equal(0, await GetSpatialIndexCountAsync(noSpatialIndexesContext, cancellationToken));
        }
        finally
        {
            await spatialContext.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static SpatialDatabaseContext CreateSpatialContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SpatialDatabaseContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new SpatialDatabaseContext(options);
    }

    private static ChangedSpatialDatabaseContext CreateChangedSpatialContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ChangedSpatialDatabaseContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new ChangedSpatialDatabaseContext(options);
    }

    private static NoSpatialIndexesDatabaseContext CreateNoSpatialIndexesContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<NoSpatialIndexesDatabaseContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.UseNetTopologySuite())
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new NoSpatialIndexesDatabaseContext(options);
    }

    private static async Task<int> GetSpatialIndexCountAsync(
        DbContext context,
        CancellationToken cancellationToken)
        => Convert.ToInt32(
            await ExecuteSpatialIndexScalarAsync(
                context,
                "COUNT(*)",
                indexName: null,
                cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<string?> GetSpatialIndexTessellationAsync(
        DbContext context,
        string indexName,
        CancellationToken cancellationToken)
        => (string?)await ExecuteSpatialIndexScalarAsync(
            context,
            "spatial_index.tessellation_scheme",
            indexName,
            cancellationToken);

    private static async Task<int> GetSpatialIndexCellsPerObjectAsync(
        DbContext context,
        string indexName,
        CancellationToken cancellationToken)
        => Convert.ToInt32(
            await ExecuteSpatialIndexScalarAsync(
                context,
                "tessellation.cells_per_object",
                indexName,
                cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<double> GetSpatialIndexBoundingBoxXMinAsync(
        DbContext context,
        string indexName,
        CancellationToken cancellationToken)
        => Convert.ToDouble(
            await ExecuteSpatialIndexScalarAsync(
                context,
                "tessellation.bounding_box_xmin",
                indexName,
                cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<object?> ExecuteSpatialIndexScalarAsync(
        DbContext context,
        string selection,
        string? indexName,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {selection}
            FROM sys.spatial_indexes AS spatial_index
            INNER JOIN sys.spatial_index_tessellations AS tessellation
                ON tessellation.object_id = spatial_index.object_id
                AND tessellation.index_id = spatial_index.index_id
            INNER JOIN sys.tables AS table_definition ON table_definition.object_id = spatial_index.object_id
            INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id = table_definition.schema_id
            WHERE schema_definition.name = N'dbo'
              AND table_definition.name = N'SpatialPlaces'
              AND (@index_name IS NULL OR spatial_index.name = @index_name);
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@index_name";
        parameter.Value = (object?)indexName ?? DBNull.Value;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static void ConfigureSpatialModel(
        ModelBuilder modelBuilder,
        int geographyCellsPerObject,
        bool includeIndexes)
    {
        var entityBuilder = modelBuilder.Entity<SpatialPlace>();
        entityBuilder.ToTable("SpatialPlaces");
        entityBuilder.HasKey(place => place.Id);
        entityBuilder.Property(place => place.GeographyLocation).HasColumnType("geography");
        entityBuilder.Property(place => place.GeometryLocation).HasColumnType("geometry");

        if (!includeIndexes)
        {
            return;
        }

        entityBuilder.HasSpatialIndex(
                place => place.GeographyLocation,
                options => options.HasCellsPerObject(geographyCellsPerObject))
            .HasDatabaseName("SIX_SpatialPlaces_GeographyLocation");
        entityBuilder.HasSpatialIndex(
                place => place.GeometryLocation,
                options => options
                    .HasBoundingBox(-1000, -500, 1000, 500)
                    .HasCellsPerObject(64))
            .HasDatabaseName("SIX_SpatialPlaces_GeometryLocation");
    }

    private sealed class SpatialDatabaseContext(DbContextOptions<SpatialDatabaseContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, geographyCellsPerObject: 32, includeIndexes: true);
    }

    private sealed class ChangedSpatialDatabaseContext(DbContextOptions<ChangedSpatialDatabaseContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, geographyCellsPerObject: 128, includeIndexes: true);
    }

    private sealed class NoSpatialIndexesDatabaseContext(DbContextOptions<NoSpatialIndexesDatabaseContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureSpatialModel(modelBuilder, geographyCellsPerObject: 32, includeIndexes: false);
    }

    private sealed class SpatialPlace
    {
        public int Id { get; set; }
        public Point GeographyLocation { get; set; } = new(0, 0) { SRID = 4326 };
        public Point GeometryLocation { get; set; } = new(0, 0);
    }
}
