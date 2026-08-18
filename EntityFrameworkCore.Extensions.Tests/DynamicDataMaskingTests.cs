using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;
using System.Reflection;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class DynamicDataMaskingTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=NotUsed";

    [Fact]
    public void DefaultMaskingFunctionGeneratesExpectedExpression()
        => Assert.Equal("default()", MaskingFunctions.Default());

    [Fact]
    public void EmailMaskingFunctionGeneratesExpectedExpression()
        => Assert.Equal("email()", MaskingFunctions.Email());

    [Fact]
    public void ParameterizedMaskingFunctionsGenerateExpectedExpressions()
    {
        Assert.Equal("random(10, 100)", MaskingFunctions.Random(10, 100));
        Assert.Equal("partial(2, \"XX-XX\", 1)", MaskingFunctions.Partial(2, "XX-XX", 1));
    }

    [Fact]
    public void PartialMaskingFunctionRejectsDoubleQuoteInPadding()
    {
        var exception = Assert.Throws<ArgumentException>(() => MaskingFunctions.Partial(1, "a\"b", 1));

        Assert.Equal("padding", exception.ParamName);
    }

    [Fact]
    public void HasDataMaskStoresAnnotationAndReturnsPropertyBuilder()
    {
        var modelBuilder = new ModelBuilder();
        var propertyBuilder = modelBuilder.Entity<SecretEntity>().Property(entity => entity.Secret);

        var result = propertyBuilder.HasDataMask(MaskingFunctions.Email());

        Assert.Same(propertyBuilder, result);
        Assert.Equal(
            MaskingFunctions.Email(),
            propertyBuilder.Metadata.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
    }

    [Fact]
    public void CreatingMaskedModelPropagatesAnnotationAndGeneratesMaskSql()
    {
        using var context = CreateMaskedContext();
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var operations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source: null, designModel.GetRelationalModel());

        var createTable = Assert.Single(operations.OfType<CreateTableOperation>());
        var secretColumn = Assert.Single(createTable.Columns, column => column.Name == "Select]");
        Assert.Equal(
            MaskingFunctions.Default(),
            secretColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);

        var commands = GenerateCommands(context, operations, designModel);
        Assert.Collection(
            commands,
            command => Assert.Contains("SCHEMA_ID(N'odd]schema')", command.CommandText, StringComparison.Ordinal),
            command => Assert.Contains("CREATE TABLE [odd]]schema].[Order]", command.CommandText, StringComparison.Ordinal),
            command => Assert.Equal(
                "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] ADD MASKED WITH (FUNCTION = N'default()');",
                command.CommandText.Trim()));
    }

    [Fact]
    public void AddingMaskGeneratesAddMaskedSql()
    {
        using var sourceContext = CreateUnmaskedContext();
        using var targetContext = CreateMaskedContext();
        var sourceModel = sourceContext.GetService<IDesignTimeModel>().Model;
        var targetModel = targetContext.GetService<IDesignTimeModel>().Model;
        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var alterColumn = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Null(alterColumn.OldColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking));
        Assert.Equal(
            MaskingFunctions.Default(),
            alterColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);

        var command = Assert.Single(GenerateCommands(targetContext, operations, targetModel));
        Assert.Equal(
            "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] ADD MASKED WITH (FUNCTION = N'default()');",
            command.CommandText.Trim());
    }

    [Fact]
    public void RemovingMaskGeneratesOneCatalogGuardedDrop()
    {
        using var sourceContext = CreateMaskedContext();
        using var targetContext = CreateUnmaskedContext();
        var sourceModel = sourceContext.GetService<IDesignTimeModel>().Model;
        var targetModel = targetContext.GetService<IDesignTimeModel>().Model;
        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var alterColumn = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Equal(
            MaskingFunctions.Default(),
            alterColumn.OldColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
        Assert.Null(alterColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking));

        var command = Assert.Single(GenerateCommands(targetContext, operations, targetModel));
        Assert.StartsWith("IF EXISTS (SELECT 1 FROM [sys].[masked_columns]", command.CommandText);
        Assert.Contains("OBJECT_ID(N'[odd]]schema].[Order]')", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("[name] = N'Select]'", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("[is_masked] = 1", command.CommandText, StringComparison.Ordinal);
        Assert.EndsWith(
            "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] DROP MASKED;",
            command.CommandText.Trim());
    }

    [Fact]
    public void RemovingMaskDuringStructuralAlterUsesSafeGuardedDrop()
    {
        using var context = CreateMaskedContext();
        var operation = new AlterColumnOperation
        {
            Schema = "odd]schema",
            Table = "Order",
            Name = "Select]",
            ClrType = typeof(string),
            ColumnType = "nvarchar(200)",
            IsNullable = true,
            OldColumn = new AddColumnOperation
            {
                Schema = "odd]schema",
                Table = "Order",
                Name = "Select]",
                ClrType = typeof(string),
                ColumnType = "nvarchar(100)",
                IsNullable = true
            }
        };
        operation.OldColumn.AddAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Default());

        var commands = GenerateCommands(context, [operation], model: null);

        Assert.Collection(
            commands,
            command => Assert.Contains(
                "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] nvarchar(200)",
                command.CommandText,
                StringComparison.Ordinal),
            command =>
            {
                Assert.Contains("FROM [sys].[masked_columns]", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("[is_masked] = 1", command.CommandText, StringComparison.Ordinal);
                Assert.Contains(
                    "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] DROP MASKED;",
                    command.CommandText,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void ChangingMaskUsesIdempotentAddWithoutPhysicalColumnAlter()
    {
        using var sourceContext = CreateMaskedContext();
        using var targetContext = CreateEmailMaskedContext();
        var sourceModel = sourceContext.GetService<IDesignTimeModel>().Model;
        var targetModel = targetContext.GetService<IDesignTimeModel>().Model;
        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var alterColumn = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Equal(
            MaskingFunctions.Default(),
            alterColumn.OldColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
        Assert.Equal(
            MaskingFunctions.Email(),
            alterColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);

        var command = Assert.Single(GenerateCommands(targetContext, operations, targetModel));
        Assert.Equal(
            "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] ADD MASKED WITH (FUNCTION = N'email()');",
            command.CommandText.Trim());
        Assert.Equal(
            MaskingFunctions.Default(),
            alterColumn.OldColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
        Assert.Equal(
            MaskingFunctions.Email(),
            alterColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
    }

    [Fact]
    public void DefaultOnlyAlterReappliesUnchangedMaskIdempotently()
    {
        using var sourceContext = CreateMaskedContext();
        using var targetContext = CreateDefaultValueMaskedContext();
        var sourceModel = sourceContext.GetService<IDesignTimeModel>().Model;
        var targetModel = targetContext.GetService<IDesignTimeModel>().Model;
        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var alterColumn = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Equal(
            alterColumn.OldColumn[AnnotationConstants.DynamicDataMasking],
            alterColumn[AnnotationConstants.DynamicDataMasking]);

        var commands = GenerateCommands(targetContext, operations, targetModel);
        Assert.NotEmpty(commands);
        Assert.DoesNotContain(commands, command => command.CommandText.Contains("DROP MASKED", StringComparison.Ordinal));
        Assert.Equal(
            "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] ADD MASKED WITH (FUNCTION = N'default()');",
            commands[^1].CommandText.Trim());
    }

    [Fact]
    public void StructuralAlterReappliesUnchangedMask()
    {
        using var sourceContext = CreateMaskedContext();
        using var targetContext = CreateSizedMaskedContext();
        var sourceModel = sourceContext.GetService<IDesignTimeModel>().Model;
        var targetModel = targetContext.GetService<IDesignTimeModel>().Model;
        var operations = targetContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            sourceModel.GetRelationalModel(),
            targetModel.GetRelationalModel());

        var alterColumn = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Equal(
            alterColumn.OldColumn[AnnotationConstants.DynamicDataMasking],
            alterColumn[AnnotationConstants.DynamicDataMasking]);

        var commands = GenerateCommands(targetContext, operations, targetModel);
        Assert.Collection(
            commands,
            command => Assert.Contains("ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]]", command.CommandText),
            command => Assert.Equal(
                "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] ADD MASKED WITH (FUNCTION = N'default()');",
                command.CommandText.Trim()));
    }

    [Fact]
    public void RecreatedColumnAddsMaskExactlyOnce()
    {
        using var context = CreateMaskedContext();
        var operation = new AlterColumnOperation
        {
            Schema = "odd]schema",
            Table = "Order",
            Name = "Select]",
            ClrType = typeof(string),
            ColumnType = "nvarchar(100)",
            IsNullable = true,
            OldColumn = new AddColumnOperation
            {
                Schema = "odd]schema",
                Table = "Order",
                Name = "Select]",
                ClrType = typeof(string),
                ColumnType = "nvarchar(100)",
                IsNullable = true,
                ComputedColumnSql = "N'computed'",
                IsStored = false
            }
        };
        operation.AddAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Email());

        var commands = GenerateCommands(context, [operation], model: null);
        var maskingCommand = Assert.Single(
            commands,
            command => command.CommandText.Contains("ADD MASKED", StringComparison.Ordinal));

        Assert.Same(commands[^1], maskingCommand);
        Assert.Equal(
            "ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]] ADD MASKED WITH (FUNCTION = N'email()');",
            maskingCommand.CommandText.Trim());
    }

    [Fact]
    public void MaskedOperationsRejectUnterminatedGeneration()
    {
        using var context = CreateGeneratorContext();
        var generator = Assert.IsType<ExtendedSqlServerMigrationsSqlGenerator>(
            context.GetService<IMigrationsSqlGenerator>());
        var dependencies = context.GetService<MigrationsSqlGeneratorDependencies>();
        var column = new AddColumnOperation
        {
            Table = "Secrets",
            Name = "Secret",
            ClrType = typeof(string),
            ColumnType = "nvarchar(100)",
            IsNullable = true
        };
        column.AddAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Default());
        var table = new CreateTableOperation { Name = "Secrets" };
        table.Columns.Add(column);

        var columnException = Assert.Throws<TargetInvocationException>(
            () => GenerateUnterminated(generator, dependencies, column));
        var tableException = Assert.Throws<TargetInvocationException>(
            () => GenerateUnterminated(generator, dependencies, table));

        Assert.IsType<InvalidOperationException>(columnException.InnerException);
        Assert.IsType<InvalidOperationException>(tableException.InnerException);
    }

    [Fact]
    public void MaskSqlDelimitsIdentifiersAndEscapesSqlLiterals()
    {
        using var context = CreateMaskedContext();
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migrationBuilder
            .AddColumn<string>(
                name: "Select]",
                schema: "odd]schema",
                table: "Order",
                type: "nvarchar(100)",
                nullable: true)
            .Annotation(AnnotationConstants.DynamicDataMasking, "partial(1, \"O'Reilly Δ\", 1)");

        var sql = GenerateSql(context, migrationBuilder.Operations, model: null);

        Assert.Contains("ALTER TABLE [odd]]schema].[Order] ALTER COLUMN [Select]]]", sql, StringComparison.Ordinal);
        Assert.Contains("FUNCTION = N'partial(1, \"O''Reilly Δ\", 1)'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidMaskingAnnotationReportsAConfigurationError()
    {
        using var context = CreateMaskedContext();
        var operation = new AddColumnOperation
        {
            Table = "Secrets",
            Name = "Secret",
            ClrType = typeof(string),
            ColumnType = "nvarchar(100)",
            IsNullable = true
        };
        operation.AddAnnotation(AnnotationConstants.DynamicDataMasking, 42);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GenerateCommands(context, [operation], model: null));

        Assert.Contains(AnnotationConstants.DynamicDataMasking, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeModelDoesNotContainMigrationMaskingAnnotation()
    {
        using var context = CreateMaskedContext();

        var runtimeColumn = context.Model.GetRelationalModel()
            .FindTable("Order", "odd]schema")!
            .FindColumn("Select]")!;
        var designColumn = context.GetService<IDesignTimeModel>().Model.GetRelationalModel()
            .FindTable("Order", "odd]schema")!
            .FindColumn("Select]")!;

        Assert.Null(runtimeColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking));
        Assert.Equal(
            MaskingFunctions.Default(),
            designColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
    }

    [Fact]
    public void ConflictingMasksOnSharedColumnAreRejected()
    {
        var options = new DbContextOptionsBuilder<ConflictingSharedColumnContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        using var context = new ConflictingSharedColumnContext(options);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _ = context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Contains("conflicting dynamic data masks", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SharedSecrets.Secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasDataMaskRejectsEmptyPattern()
    {
        var modelBuilder = new ModelBuilder();
        var propertyBuilder = modelBuilder.Entity<SecretEntity>().Property(entity => entity.Secret);

        Assert.Throws<ArgumentException>(() => propertyBuilder.HasDataMask(" "));
    }

    private static MaskedContext CreateMaskedContext()
    {
        var options = new DbContextOptionsBuilder<MaskedContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new MaskedContext(options);
    }

    private static EmailMaskedContext CreateEmailMaskedContext()
    {
        var options = new DbContextOptionsBuilder<EmailMaskedContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new EmailMaskedContext(options);
    }

    private static DefaultValueMaskedContext CreateDefaultValueMaskedContext()
    {
        var options = new DbContextOptionsBuilder<DefaultValueMaskedContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new DefaultValueMaskedContext(options);
    }

    private static SizedMaskedContext CreateSizedMaskedContext()
    {
        var options = new DbContextOptionsBuilder<SizedMaskedContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new SizedMaskedContext(options);
    }

    private static UnmaskedContext CreateUnmaskedContext()
    {
        var options = new DbContextOptionsBuilder<UnmaskedContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new UnmaskedContext(options);
    }

    private static DbContext CreateGeneratorContext()
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseSqlServer(ConnectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new DbContext(options);
    }

    private static void GenerateUnterminated(
        ExtendedSqlServerMigrationsSqlGenerator generator,
        MigrationsSqlGeneratorDependencies dependencies,
        MigrationOperation operation)
    {
        var generateMethod = typeof(ExtendedSqlServerMigrationsSqlGenerator).GetMethod(
            nameof(IMigrationsSqlGenerator.Generate),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                operation.GetType(),
                typeof(IModel),
                typeof(MigrationCommandListBuilder),
                typeof(bool)
            ],
            modifiers: null);

        Assert.NotNull(generateMethod);
        generateMethod.Invoke(
            generator,
            [operation, null, new MigrationCommandListBuilder(dependencies), false]);
    }

    private static IReadOnlyList<MigrationCommand> GenerateCommands(
        DbContext context,
        IReadOnlyList<MigrationOperation> operations,
        IModel? model)
        => context.GetService<IMigrationsSqlGenerator>().Generate(operations, model);

    private static string GenerateSql(DbContext context, IReadOnlyList<MigrationOperation> operations, IModel? model)
        => string.Join(
            Environment.NewLine,
            GenerateCommands(context, operations, model).Select(command => command.CommandText));

    private static void ConfigureModel(
        ModelBuilder modelBuilder,
        string? maskingFunction,
        string? defaultValue = null,
        int? maxLength = null)
    {
        var entityBuilder = modelBuilder.Entity<SecretEntity>();
        entityBuilder.ToTable("Order", "odd]schema");
        entityBuilder.HasKey(entity => entity.Id);
        var secretProperty = entityBuilder.Property(entity => entity.Secret).HasColumnName("Select]");
        if (maxLength is not null)
        {
            secretProperty.HasMaxLength(maxLength.Value);
        }

        if (maskingFunction is not null)
        {
            secretProperty.HasDataMask(maskingFunction);
        }

        if (defaultValue is not null)
        {
            secretProperty.HasDefaultValue(defaultValue);
        }
    }

    private sealed class MaskedContext(DbContextOptions<MaskedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Default());
    }

    private sealed class EmailMaskedContext(DbContextOptions<EmailMaskedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Email());
    }

    private sealed class DefaultValueMaskedContext(DbContextOptions<DefaultValueMaskedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Default(), "fallback");
    }

    private sealed class SizedMaskedContext(DbContextOptions<SizedMaskedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Default(), maxLength: 100);
    }

    private sealed class UnmaskedContext(DbContextOptions<UnmaskedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureModel(modelBuilder, maskingFunction: null);
    }

    private sealed class SecretEntity
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class ConflictingSharedColumnContext(DbContextOptions<ConflictingSharedColumnContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedColumnPrincipal>(entityBuilder =>
            {
                entityBuilder.ToTable("SharedSecrets");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.Property(entity => entity.Secret)
                    .HasColumnName("Secret")
                    .HasDataMask(MaskingFunctions.Default());
                entityBuilder.HasOne(entity => entity.Details)
                    .WithOne()
                    .HasForeignKey<SharedColumnDetails>(entity => entity.Id);
            });

            modelBuilder.Entity<SharedColumnDetails>(entityBuilder =>
            {
                entityBuilder.ToTable("SharedSecrets");
                entityBuilder.HasKey(entity => entity.Id);
                entityBuilder.Property(entity => entity.Secret)
                    .HasColumnName("Secret")
                    .HasDataMask(MaskingFunctions.Email());
            });
        }
    }

    private sealed class SharedColumnPrincipal
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
        public SharedColumnDetails Details { get; set; } = null!;
    }

    private sealed class SharedColumnDetails
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }

}
