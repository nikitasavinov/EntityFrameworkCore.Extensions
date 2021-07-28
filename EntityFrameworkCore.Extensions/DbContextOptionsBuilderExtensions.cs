using EntityFrameworkCore.Extensions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EntityFrameworkCore.Extensions
{
    public static class DbContextOptionsBuilderExtensions
    {
        /// <summary>
        /// Register services necessary for enabling dynamic data masking (and more features in the future)
        /// </summary>
        public static DbContextOptionsBuilder UseEntityFrameworkCoreExtensions(this DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ReplaceService<IMigrationsSqlGenerator, ExtendedMigrationSqlServerGenerator>();
            optionsBuilder.ReplaceService<IRelationalAnnotationProvider, ExtendedSqlServerAnnotationProvider>();

            return optionsBuilder;
        }
    }
}
