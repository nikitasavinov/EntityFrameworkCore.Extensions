using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EntityFrameworkCore.Extensions
{
    public static class MigrationBuilderExtensions
    {
        /// <summary>
        /// Executes raw sql from file. File property "Copy to output directory" should be set
        /// to "Copy always"
        /// </summary>
        /// <param name="migrationBuilder"></param>
        /// <param name="path">The path to the file</param>
        /// <returns></returns>
        public static MigrationBuilder SqlFile(this MigrationBuilder migrationBuilder, string path)
        {
            var sql = File.ReadAllText(path);
            migrationBuilder.Sql(sql);

            return migrationBuilder;
        }
    }
}
