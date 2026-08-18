using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class SqlServerIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "EFCORE_EXTENSIONS_SQLSERVER";

    public static bool HasSqlServerConnectionString
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable));

    [Fact(
        Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.",
        SkipUnless = nameof(HasSqlServerConnectionString),
        Timeout = 120_000)]
    public async Task CreatingMaskedModelAppliesMaskAndSqlServerEnforcesIt()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var databaseName = $"EfCoreExtensions_{Guid.NewGuid():N}";
        var connectionString = await CreateDatabaseConnectionStringAsync(databaseName, cancellationToken);
        await using var context = CreateMaskedContext(connectionString);

        try
        {
            Assert.True(await context.Database.EnsureCreatedAsync(cancellationToken));

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            var maskingFunction = (string?)await ExecuteScalarAsync(
                connection,
                """
                SELECT column_definition.masking_function
                FROM sys.masked_columns AS column_definition
                INNER JOIN sys.tables AS table_definition ON table_definition.object_id = column_definition.object_id
                INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id = table_definition.schema_id
                WHERE schema_definition.name = N'dbo'
                  AND table_definition.name = N'MaskedCustomers'
                  AND column_definition.name = N'Email';
                """,
                cancellationToken);
            Assert.Equal("email()", maskingFunction);

            await ExecuteNonQueryAsync(
                connection,
                "INSERT INTO [dbo].[MaskedCustomers] ([Email]) VALUES (N'alice@example.com');",
                cancellationToken);

            var unmaskedEmail = (string?)await ExecuteScalarAsync(
                connection,
                "SELECT TOP (1) [Email] FROM [dbo].[MaskedCustomers];",
                cancellationToken);
            Assert.Equal("alice@example.com", unmaskedEmail);

            await ExecuteNonQueryAsync(
                connection,
                """
                CREATE USER [MaskReader] WITHOUT LOGIN;
                GRANT SELECT ON OBJECT::[dbo].[MaskedCustomers] TO [MaskReader];
                EXECUTE AS USER = N'MaskReader';
                """,
                cancellationToken);
            try
            {
                var maskedEmail = (string?)await ExecuteScalarAsync(
                    connection,
                    "SELECT TOP (1) [Email] FROM [dbo].[MaskedCustomers];",
                    cancellationToken);
                Assert.Equal("aXXX@XXXX.com", maskedEmail);
            }
            finally
            {
                await ExecuteNonQueryAsync(connection, "REVERT;", cancellationToken);
            }
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact(
        Skip = $"Set {ConnectionStringEnvironmentVariable} to run SQL Server integration tests.",
        SkipUnless = nameof(HasSqlServerConnectionString),
        Timeout = 120_000)]
    public async Task ModelDifferencesAddChangeAndRemoveMaskOnExistingColumn()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var databaseName = $"EfCoreExtensions_{Guid.NewGuid():N}";
        var connectionString = await CreateDatabaseConnectionStringAsync(databaseName, cancellationToken);
        await using var unmaskedContext = CreateUnmaskedContext(connectionString);
        await using var maskedContext = CreateMaskedContext(connectionString);
        await using var defaultValueMaskedContext = CreateDefaultValueMaskedContext(connectionString);
        await using var resizedMaskedContext = CreateResizedMaskedContext(connectionString);
        await using var defaultMaskedContext = CreateDefaultMaskedContext(connectionString);
        await using var resizedUnmaskedContext = CreateResizedUnmaskedContext(connectionString);

        try
        {
            Assert.True(await unmaskedContext.Database.EnsureCreatedAsync(cancellationToken));

            var unmaskedModel = unmaskedContext.GetService<IDesignTimeModel>().Model;
            var maskedModel = maskedContext.GetService<IDesignTimeModel>().Model;
            var addMaskOperations = maskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                unmaskedModel.GetRelationalModel(),
                maskedModel.GetRelationalModel());
            Assert.Single(addMaskOperations.OfType<AlterColumnOperation>());
            await ExecuteMigrationOperationsAsync(maskedContext, addMaskOperations, maskedModel, cancellationToken);
            Assert.Equal("email()", await GetMaskingFunctionAsync(maskedContext, cancellationToken));

            var defaultValueMaskedModel = defaultValueMaskedContext.GetService<IDesignTimeModel>().Model;
            var defaultOnlyOperations = defaultValueMaskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                maskedModel.GetRelationalModel(),
                defaultValueMaskedModel.GetRelationalModel());
            Assert.Single(defaultOnlyOperations.OfType<AlterColumnOperation>());
            var defaultOnlyCommands = defaultValueMaskedContext.GetService<IMigrationsSqlGenerator>()
                .Generate(defaultOnlyOperations, defaultValueMaskedModel);
            Assert.DoesNotContain(
                defaultOnlyCommands,
                command => command.CommandText.Contains("DROP MASKED", StringComparison.Ordinal));
            Assert.Contains(
                defaultOnlyCommands,
                command => command.CommandText.Contains("ADD MASKED", StringComparison.Ordinal));
            await ExecuteMigrationOperationsAsync(
                defaultValueMaskedContext,
                defaultOnlyOperations,
                defaultValueMaskedModel,
                cancellationToken);
            Assert.Equal("email()", await GetMaskingFunctionAsync(defaultValueMaskedContext, cancellationToken));

            var resizedMaskedModel = resizedMaskedContext.GetService<IDesignTimeModel>().Model;
            var resizeOperations = resizedMaskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                defaultValueMaskedModel.GetRelationalModel(),
                resizedMaskedModel.GetRelationalModel());
            Assert.Single(resizeOperations.OfType<AlterColumnOperation>());
            await ExecuteMigrationOperationsAsync(
                resizedMaskedContext,
                resizeOperations,
                resizedMaskedModel,
                cancellationToken);
            Assert.Equal("email()", await GetMaskingFunctionAsync(resizedMaskedContext, cancellationToken));

            var defaultMaskedModel = defaultMaskedContext.GetService<IDesignTimeModel>().Model;
            var changeMaskOperations = defaultMaskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                resizedMaskedModel.GetRelationalModel(),
                defaultMaskedModel.GetRelationalModel());
            Assert.Single(changeMaskOperations.OfType<AlterColumnOperation>());
            await ExecuteMigrationOperationsAsync(
                defaultMaskedContext,
                changeMaskOperations,
                defaultMaskedModel,
                cancellationToken);
            Assert.Equal("default()", await GetMaskingFunctionAsync(defaultMaskedContext, cancellationToken));

            var removeMaskOperations = unmaskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                defaultMaskedModel.GetRelationalModel(),
                unmaskedModel.GetRelationalModel());
            Assert.Single(removeMaskOperations.OfType<AlterColumnOperation>());
            await ExecuteMigrationOperationsAsync(unmaskedContext, removeMaskOperations, unmaskedModel, cancellationToken);
            Assert.Null(await GetMaskingFunctionAsync(unmaskedContext, cancellationToken));

            // Replaying a removal against an already-unmasked column must remain a safe no-op.
            await ExecuteMigrationOperationsAsync(unmaskedContext, removeMaskOperations, unmaskedModel, cancellationToken);
            Assert.Null(await GetMaskingFunctionAsync(unmaskedContext, cancellationToken));

            var reAddMaskOperations = defaultMaskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                unmaskedModel.GetRelationalModel(),
                defaultMaskedModel.GetRelationalModel());
            await ExecuteMigrationOperationsAsync(
                defaultMaskedContext,
                reAddMaskOperations,
                defaultMaskedModel,
                cancellationToken);
            Assert.Equal("default()", await GetMaskingFunctionAsync(defaultMaskedContext, cancellationToken));

            var resizedUnmaskedModel = resizedUnmaskedContext.GetService<IDesignTimeModel>().Model;
            var structuralRemoveOperations = resizedUnmaskedContext.GetService<IMigrationsModelDiffer>().GetDifferences(
                defaultMaskedModel.GetRelationalModel(),
                resizedUnmaskedModel.GetRelationalModel());
            Assert.Single(structuralRemoveOperations.OfType<AlterColumnOperation>());
            var structuralRemoveCommands = resizedUnmaskedContext.GetService<IMigrationsSqlGenerator>()
                .Generate(structuralRemoveOperations, resizedUnmaskedModel);
            Assert.Contains(
                structuralRemoveCommands,
                command => command.CommandText.Contains("ALTER COLUMN", StringComparison.Ordinal)
                    && !command.CommandText.Contains("DROP MASKED", StringComparison.Ordinal));
            Assert.Contains(
                structuralRemoveCommands,
                command => command.CommandText.Contains("[sys].[masked_columns]", StringComparison.Ordinal)
                    && command.CommandText.Contains("DROP MASKED", StringComparison.Ordinal));
            await ExecuteMigrationOperationsAsync(
                resizedUnmaskedContext,
                structuralRemoveOperations,
                resizedUnmaskedModel,
                cancellationToken);
            Assert.Null(await GetMaskingFunctionAsync(resizedUnmaskedContext, cancellationToken));
        }
        finally
        {
            await unmaskedContext.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static async Task<string> CreateDatabaseConnectionStringAsync(
        string databaseName,
        CancellationToken cancellationToken)
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;
        var masterConnectionString = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        }.ConnectionString;

        await WaitForSqlServerAsync(masterConnectionString, cancellationToken);

        return new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
    }

    private static async Task WaitForSqlServerAsync(string connectionString, CancellationToken cancellationToken)
    {
        const int maximumAttempts = 60;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (SqlException) when (attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private static MaskedDatabaseContext CreateMaskedContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MaskedDatabaseContext>()
            .UseSqlServer(connectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new MaskedDatabaseContext(options);
    }

    private static UnmaskedDatabaseContext CreateUnmaskedContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<UnmaskedDatabaseContext>()
            .UseSqlServer(connectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new UnmaskedDatabaseContext(options);
    }

    private static DefaultMaskedDatabaseContext CreateDefaultMaskedContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DefaultMaskedDatabaseContext>()
            .UseSqlServer(connectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new DefaultMaskedDatabaseContext(options);
    }

    private static DefaultValueMaskedDatabaseContext CreateDefaultValueMaskedContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DefaultValueMaskedDatabaseContext>()
            .UseSqlServer(connectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new DefaultValueMaskedDatabaseContext(options);
    }

    private static ResizedMaskedDatabaseContext CreateResizedMaskedContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ResizedMaskedDatabaseContext>()
            .UseSqlServer(connectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new ResizedMaskedDatabaseContext(options);
    }

    private static ResizedUnmaskedDatabaseContext CreateResizedUnmaskedContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ResizedUnmaskedDatabaseContext>()
            .UseSqlServer(connectionString)
            .UseEntityFrameworkCoreExtensions()
            .Options;
        return new ResizedUnmaskedDatabaseContext(options);
    }

    private static async Task ExecuteMigrationOperationsAsync(
        DbContext context,
        IReadOnlyList<MigrationOperation> operations,
        IModel model,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, model);
        foreach (var migrationCommand in commands)
        {
            await ExecuteNonQueryAsync(connection, migrationCommand.CommandText, cancellationToken);
        }
    }

    private static async Task<string?> GetMaskingFunctionAsync(DbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return (string?)await ExecuteScalarAsync(
            connection,
            """
            SELECT column_definition.masking_function
            FROM sys.masked_columns AS column_definition
            INNER JOIN sys.tables AS table_definition ON table_definition.object_id = column_definition.object_id
            INNER JOIN sys.schemas AS schema_definition ON schema_definition.schema_id = table_definition.schema_id
            WHERE schema_definition.name = N'dbo'
              AND table_definition.name = N'MaskedCustomers'
              AND column_definition.name = N'Email'
              AND column_definition.is_masked = 1;
            """,
            cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ConfigureModel(
        ModelBuilder modelBuilder,
        string? maskingFunction,
        string? defaultValue = null,
        int maxLength = 320)
    {
        var entityBuilder = modelBuilder.Entity<MaskedCustomer>();
        entityBuilder.ToTable("MaskedCustomers");
        entityBuilder.HasKey(customer => customer.Id);
        entityBuilder.Property(customer => customer.Id).ValueGeneratedOnAdd();
        var emailProperty = entityBuilder.Property(customer => customer.Email).HasMaxLength(maxLength);
        if (maskingFunction is not null)
        {
            emailProperty.HasDataMask(maskingFunction);
        }

        if (defaultValue is not null)
        {
            emailProperty.HasDefaultValue(defaultValue);
        }
    }

    private sealed class MaskedDatabaseContext(DbContextOptions<MaskedDatabaseContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Email());
    }

    private sealed class DefaultMaskedDatabaseContext(DbContextOptions<DefaultMaskedDatabaseContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Default());
    }

    private sealed class DefaultValueMaskedDatabaseContext(DbContextOptions<DefaultValueMaskedDatabaseContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Email(), "fallback@example.com");
    }

    private sealed class ResizedMaskedDatabaseContext(DbContextOptions<ResizedMaskedDatabaseContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, MaskingFunctions.Email(), "fallback@example.com", maxLength: 200);
    }

    private sealed class UnmaskedDatabaseContext(DbContextOptions<UnmaskedDatabaseContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, maskingFunction: null);
    }

    private sealed class ResizedUnmaskedDatabaseContext(DbContextOptions<ResizedUnmaskedDatabaseContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => ConfigureModel(modelBuilder, maskingFunction: null, maxLength: 200);
    }

    private sealed class MaskedCustomer
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
