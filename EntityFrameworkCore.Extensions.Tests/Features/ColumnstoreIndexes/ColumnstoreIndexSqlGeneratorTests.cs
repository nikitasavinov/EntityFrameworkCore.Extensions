using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using static EntityFrameworkCore.Extensions.Tests.ColumnstoreTestContext;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class ColumnstoreIndexSqlGeneratorTests
{
    private static readonly string[] IncludedColumns = ["Amount"];

    [Fact]
    public void ModelGeneratesFilteredColumnstoreSqlWithDelimitedIdentifiers()
    {
        using var context = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.ToTable("Sales]", "odd]schema");
            entity.Property(sale => sale.Amount).HasColumnName("Total]");
            AnalyticsIndex(entity).HasDatabaseName("NCCI_Analytics]").HasFilter("[TenantId] > 0");
        });
        var model = context.DesignModel;
        var operations = context.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var createIndex = Assert.Single(operations.OfType<CreateIndexOperation>());

        Assert.Equal(true, createIndex[AnnotationConstants.ColumnstoreIndex]);
        var command = Assert.Single(Generate(context, [createIndex], model));
        Assert.Equal(
            "CREATE NONCLUSTERED COLUMNSTORE INDEX [NCCI_Analytics]]] ON [odd]]schema].[Sales]]] ([TenantId], [Total]]], [OrderedAt]) WHERE [TenantId] > 0;",
            command.CommandText.Trim());
    }

    [Theory]
    [InlineData("columns")]
    [InlineData("filter")]
    [InlineData("rowstore")]
    public void ChangingIndexDefinitionDropsAndRecreatesIndex(string change)
    {
        using var source = new ColumnstoreTestContext();
        using var target = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            switch (change)
            {
                case "columns":
                    entity.Metadata.RemoveIndex(AnalyticsIndex(entity).Metadata);
                    entity.HasColumnstoreIndex(sale => new { sale.TenantId, sale.Amount }, "Analytics")
                        .HasDatabaseName("NCCI_Sales_Analytics");
                    break;
                case "filter": AnalyticsIndex(entity).HasFilter("[TenantId] > 0"); break;
                case "rowstore": AnalyticsIndex(entity).HasAnnotation(AnnotationConstants.ColumnstoreIndex, false); break;
            }
        });

        var operations = Differences(source, target);
        Assert.Collection(operations,
            operation => Assert.IsType<DropIndexOperation>(operation),
            operation => Assert.IsType<CreateIndexOperation>(operation));
        var sql = string.Join("\n", Generate(target, operations, target.DesignModel).Select(command => command.CommandText));
        Assert.Contains("DROP INDEX [NCCI_Sales_Analytics]", sql, StringComparison.Ordinal);
        Assert.Contains(change == "rowstore" ? "CREATE INDEX" : "CREATE NONCLUSTERED COLUMNSTORE INDEX", sql, StringComparison.Ordinal);

        // The reverse model difference is the migration's Down path.
        var reverseOperations = Differences(target, source);
        Assert.Single(reverseOperations.OfType<DropIndexOperation>());
        Assert.Single(reverseOperations.OfType<CreateIndexOperation>());
        Assert.Contains(Generate(source, reverseOperations, source.DesignModel),
            command => command.CommandText.Contains("CREATE NONCLUSTERED COLUMNSTORE INDEX", StringComparison.Ordinal));
    }

    [Fact]
    public void RenamingAndRemovingUseStandardEfOperations()
    {
        using var source = new ColumnstoreTestContext();
        using var renamed = new ColumnstoreTestContext(modelBuilder =>
            AnalyticsIndex(modelBuilder.Entity<ColumnstoreSale>()).HasDatabaseName("Renamed"));
        using var removed = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.Metadata.RemoveIndex(AnalyticsIndex(entity).Metadata);
        });

        var rename = Assert.IsType<RenameIndexOperation>(Assert.Single(Differences(source, renamed)));
        Assert.Equal("Renamed", rename.NewName);
        Assert.Contains("sp_rename", Assert.Single(Generate(renamed, [rename], renamed.DesignModel)).CommandText, StringComparison.Ordinal);
        var drop = Assert.IsType<DropIndexOperation>(Assert.Single(Differences(renamed, removed)));
        Assert.Equal("DROP INDEX [Renamed] ON [reporting].[Sales];",
            Assert.Single(Generate(removed, [drop], removed.DesignModel)).CommandText.Trim());
    }

    [Fact]
    public void AlteringIndexedColumnRebuildsColumnstoreIndex()
    {
        using var source = DescriptionIndexContext(200);
        using var target = DescriptionIndexContext(100);
        var operations = Differences(source, target);
        var sql = string.Join("\n", Generate(target, operations, target.DesignModel).Select(command => command.CommandText));

        Assert.Contains("DROP INDEX [NCCI_Description]", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN [Description] nvarchar(100)", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE NONCLUSTERED COLUMNSTORE INDEX [NCCI_Description]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE INDEX [NCCI_Description]", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unique", "cannot be unique")]
    [InlineData("descending", "cannot specify sort order")]
    [InlineData("empty", "between 1 and 1024")]
    [InlineData("too-many", "between 1 and 1024")]
    [InlineData("spatial", "both columnstore and spatial")]
    [InlineData("invalid-marker", "must contain a Boolean")]
    [InlineData("clustered", "SqlServer:Clustered")]
    [InlineData("include", "SqlServer:Include")]
    [InlineData("online", "SqlServer:Online")]
    [InlineData("unknown-option", "SqlServer:FutureOption")]
    [InlineData("unknown-disabled-option", "SqlServer:FutureOption")]
    [InlineData("memory", "memory-optimized table")]
    public void InvalidRawMigrationOperationsAreRejected(string option, string expectedMessage)
    {
        using var context = new ColumnstoreTestContext();
        var operation = new CreateIndexOperation { Name = "NCCI_Test", Table = "Sales", Columns = ["TenantId"] };
        operation.AddAnnotation(AnnotationConstants.ColumnstoreIndex, true);
        switch (option)
        {
            case "unique": operation.IsUnique = true; break;
            case "descending": operation.IsDescending = []; break;
            case "empty": operation.Columns = []; break;
            case "too-many": operation.Columns = Enumerable.Range(0, 1025).Select(i => $"Column{i}").ToArray(); break;
            case "spatial": operation.AddAnnotation(AnnotationConstants.SpatialIndex, true); break;
            case "invalid-marker": operation[AnnotationConstants.ColumnstoreIndex] = "yes"; break;
            case "clustered": operation.AddAnnotation("SqlServer:Clustered", true); break;
            case "include": operation.AddAnnotation("SqlServer:Include", IncludedColumns); break;
            case "online": operation.AddAnnotation("SqlServer:Online", true); break;
            case "unknown-option": operation.AddAnnotation("SqlServer:FutureOption", true); break;
            case "unknown-disabled-option": operation.AddAnnotation("SqlServer:FutureOption", false); break;
            case "memory": operation.AddAnnotation("SqlServer:MemoryOptimized", true); break;
        }

        var exception = Assert.Throws<InvalidOperationException>(() => Generate(context, [operation], model: null));
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("max", "unsupported store type")]
    [InlineData("xml", "unsupported store type")]
    [InlineData("computed", "computed column")]
    [InlineData("sparse", "sparse column")]
    [InlineData("memory", "memory-optimized table")]
    [InlineData("second-index", "only one columnstore index")]
    public void RawMigrationOperationsValidateAvailableModelMetadata(string option, string expectedMessage)
    {
        using var context = new ColumnstoreTestContext(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            if (option != "second-index")
            {
                entity.Metadata.RemoveIndex(AnalyticsIndex(entity).Metadata);
            }

            switch (option)
            {
                case "max": entity.Property(sale => sale.Description).HasColumnType("nvarchar(max)"); break;
                case "xml": entity.Property(sale => sale.Description).HasColumnType("xml"); break;
                case "computed": entity.Property(sale => sale.Description).HasComputedColumnSql("N'test'"); break;
                case "sparse": entity.Property(sale => sale.Description).IsRequired(false).IsSparse(); break;
                case "memory": entity.ToTable(table => table.IsMemoryOptimized()); break;
            }
        });
        var model = context.DesignModel;
        // The model has no columnstore marker for this index: the migration was hand-written.
        var operation = new CreateIndexOperation
        {
            Name = "NCCI_Manual", Table = "Sales", Schema = "reporting", Columns = ["Description"]
        };
        operation.AddAnnotation(AnnotationConstants.ColumnstoreIndex, true);

        var exception = Assert.Throws<InvalidOperationException>(() => Generate(context, [operation], model));
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SqlServer:Clustered")]
    [InlineData("SqlServer:Online")]
    [InlineData("SqlServer:SortInTempDb")]
    [InlineData("SqlServer:MemoryOptimized")]
    public void ExplicitlyDisabledRawOptionsLeaveSqlUnchanged(string annotationName)
    {
        using var context = new ColumnstoreTestContext();
        var operation = new CreateIndexOperation { Name = "NCCI_Test", Table = "Sales", Columns = ["TenantId"] };
        operation.AddAnnotation(AnnotationConstants.ColumnstoreIndex, true);
        var expectedSql = Assert.Single(Generate(context, [operation], model: null)).CommandText;
        operation.AddAnnotation(annotationName, false);

        Assert.Equal(expectedSql, Assert.Single(Generate(context, [operation], model: null)).CommandText);
    }

    [Fact]
    public void ExplicitlyDisabledModelOptionsLeaveSqlUnchanged()
    {
        using var original = new ColumnstoreTestContext();
        using var configured = new ColumnstoreTestContext(modelBuilder =>
            AnalyticsIndex(modelBuilder.Entity<ColumnstoreSale>())
                .IsClustered(false).IsCreatedOnline(false).SortInTempDb(false));
        var originalIndex = Assert.Single(original.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, original.DesignModel.GetRelationalModel()).OfType<CreateIndexOperation>());
        var configuredIndex = Assert.Single(configured.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, configured.DesignModel.GetRelationalModel()).OfType<CreateIndexOperation>());

        Assert.Equal(
            Assert.Single(Generate(original, [originalIndex], original.DesignModel)).CommandText,
            Assert.Single(Generate(configured, [configuredIndex], configured.DesignModel)).CommandText);
    }

    [Fact]
    public void IdempotentFilteredIndexUsesProviderDynamicSqlAndEscapesLiterals()
    {
        using var context = new ColumnstoreTestContext();
        var operation = new CreateIndexOperation
        {
            Name = "NCCI_Test",
            Table = "Sales",
            Columns = ["Description"],
            Filter = "[Description] = N'closed'"
        };
        operation.AddAnnotation(AnnotationConstants.ColumnstoreIndex, true);
        operation.AddAnnotation("SqlServer:Clustered", false);

        var command = Assert.Single(context.GetService<IMigrationsSqlGenerator>()
            .Generate([operation], model: null, MigrationsSqlGenerationOptions.Idempotent));

        Assert.Equal(
            "EXEC(N'CREATE NONCLUSTERED COLUMNSTORE INDEX [NCCI_Test] ON [Sales] ([Description]) WHERE [Description] = N''closed''');",
            command.CommandText.Trim());
    }

    [Fact]
    public void FalseMarkerLeavesOrdinaryIndexSqlUnchanged()
    {
        using var context = new ColumnstoreTestContext();
        var operation = new CreateIndexOperation { Name = "IX_Test", Table = "Sales", Columns = ["TenantId"] };
        operation.AddAnnotation(AnnotationConstants.ColumnstoreIndex, false);

        Assert.Equal("CREATE INDEX [IX_Test] ON [Sales] ([TenantId]);",
            Assert.Single(Generate(context, [operation], model: null)).CommandText.Trim());
    }

    private static IReadOnlyList<MigrationCommand> Generate(
        DbContext context, IReadOnlyList<MigrationOperation> operations, IModel? model)
        => context.GetService<IMigrationsSqlGenerator>().Generate(operations, model);

    private static ColumnstoreTestContext DescriptionIndexContext(int maxLength)
        => new(modelBuilder =>
        {
            var entity = modelBuilder.Entity<ColumnstoreSale>();
            entity.Metadata.RemoveIndex(AnalyticsIndex(entity).Metadata);
            entity.Property(sale => sale.Description).HasMaxLength(maxLength);
            entity.HasColumnstoreIndex(sale => sale.Description).HasDatabaseName("NCCI_Description");
        });
}
