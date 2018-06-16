using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EntityFrameworkCore.Extensions
{
    public static class DatabaseFacadeExtensions
    {
        /// <summary>
        /// Migrate database to the latest version if supported by underlying database provider
        /// (so won't fail for InMemory database provider)
        /// </summary>
        /// <param name="databaseFacade"></param>
        public static void MigrateIfSupported(this DatabaseFacade databaseFacade)
        {
            var serviceProvider = databaseFacade.GetService<IServiceProvider>();
            if (serviceProvider.GetService(typeof(IMigrator)) is IMigrator migrator)
            {
                migrator.Migrate();
            }
        }

        /// <summary>
        /// Migrate database to the latest version if supported by underlying database provider
        /// (so won't fail for InMemory database provider)
        /// </summary>
        /// <param name="databaseFacade"></param>
        public static async Task MigrateIfSupportedAsync(this DatabaseFacade databaseFacade)
        {
            var serviceProvider = databaseFacade.GetService<IServiceProvider>();
            if (serviceProvider.GetService(typeof(IMigrator)) is IMigrator migrator)
            {
                await migrator.MigrateAsync();
            }
        }
    }
}
