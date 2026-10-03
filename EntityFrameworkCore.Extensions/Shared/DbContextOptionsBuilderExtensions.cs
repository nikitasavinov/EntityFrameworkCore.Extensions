using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Configures EntityFrameworkCore.Extensions services on a <see cref="DbContextOptionsBuilder" />.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Registers EntityFrameworkCore.Extensions services for SQL Server.
    /// Registration remains inert when the final provider is non-relational.
    /// </summary>
    /// <param name="optionsBuilder">The context options builder.</param>
    /// <returns>The same builder so that configuration calls can be chained.</returns>
    public static DbContextOptionsBuilder UseEntityFrameworkCoreExtensions(this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        RegisterServices(optionsBuilder);

        return optionsBuilder;
    }

    /// <summary>
    /// Registers EntityFrameworkCore.Extensions services for SQL Server.
    /// Registration remains inert when the final provider is non-relational.
    /// </summary>
    /// <typeparam name="TContext">The context type being configured.</typeparam>
    /// <param name="optionsBuilder">The context options builder.</param>
    /// <returns>The same builder so that configuration calls can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseEntityFrameworkCoreExtensions<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        RegisterServices(optionsBuilder);

        return optionsBuilder;
    }

    private static void RegisterServices(DbContextOptionsBuilder optionsBuilder)
    {
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
            .AddOrUpdateExtension(new EntityFrameworkCoreExtensionsOptionsExtension());

        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, ExtendedSqlServerMigrationsSqlGenerator>();
        optionsBuilder.ReplaceService<IRelationalAnnotationProvider, ExtendedSqlServerAnnotationProvider>();
        optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, ExtendedSqlServerQuerySqlGeneratorFactory>();
        optionsBuilder.ReplaceService<IRelationalParameterBasedSqlProcessorFactory, ExtendedSqlServerParameterBasedSqlProcessorFactory>();
        optionsBuilder.ReplaceService<IQueryTranslationPreprocessorFactory, ExtendedQueryTranslationPreprocessorFactory>();

        // Hint calls inside compiled queries and subqueries have to be rewritten before EF extracts parameters.
        // Query filters are added later, inside the provider preprocessor, so that preprocessor is wrapped rather
        // than replaced and the remaining calls are registered after it runs.
#pragma warning disable EF1001
        optionsBuilder.ReplaceService<IQueryCompiler, ExtendedQueryCompiler>();
#pragma warning restore EF1001
    }
}
