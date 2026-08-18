using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

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
    }
}
