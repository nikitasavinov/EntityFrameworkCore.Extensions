using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Provides migration helpers for <see cref="DatabaseFacade" />.
/// </summary>
public static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Migrates the database to the latest version when the configured provider supports migrations.
    /// </summary>
    /// <param name="databaseFacade">The database facade.</param>
    public static void MigrateIfSupported(this DatabaseFacade databaseFacade)
    {
        ArgumentNullException.ThrowIfNull(databaseFacade);

        if (GetMigrator(databaseFacade) is { } migrator)
        {
            migrator.Migrate();
        }
    }

    /// <summary>
    /// Asynchronously migrates the database to the latest version when the configured provider supports migrations.
    /// </summary>
    /// <param name="databaseFacade">The database facade.</param>
    /// <returns>A task representing the asynchronous migration.</returns>
    public static Task MigrateIfSupportedAsync(this DatabaseFacade databaseFacade)
        => MigrateIfSupportedAsync(databaseFacade, CancellationToken.None);

    /// <summary>
    /// Asynchronously migrates the database to the latest version when the configured provider supports migrations.
    /// </summary>
    /// <param name="databaseFacade">The database facade.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous migration.</returns>
    public static Task MigrateIfSupportedAsync(
        this DatabaseFacade databaseFacade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(databaseFacade);

        if (GetMigrator(databaseFacade) is { } migrator)
        {
            return migrator.MigrateAsync(cancellationToken: cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static IMigrator? GetMigrator(DatabaseFacade databaseFacade)
        => databaseFacade.GetService<IServiceProvider>().GetService(typeof(IMigrator)) as IMigrator;
}
