using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.IO;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests
{
    public class MigrationBuilderExtensionsTests
    {
        [Fact]
        public void SqlFileIncludesSqlInMigration()
        {
            var migrationBuilder = new MigrationBuilder("");
            var path = Path.Combine(AppContext.BaseDirectory, "CustomSql", "TestSql.sql");

            var result = migrationBuilder.SqlFile(path);

            Assert.Same(migrationBuilder, result);
            Assert.Single(migrationBuilder.Operations);
            Assert.IsType<SqlOperation>(migrationBuilder.Operations[0]);
            Assert.Equal("PRINT 'test1234'", ((SqlOperation)migrationBuilder.Operations[0]).Sql);
        }

        [Fact]
        public void SqlFileRejectsEmptyPath()
        {
            var migrationBuilder = new MigrationBuilder("");

            Assert.Throws<ArgumentException>(() => migrationBuilder.SqlFile(" "));
        }

        [Fact]
        public void SqlFileReportsMissingFile()
        {
            var migrationBuilder = new MigrationBuilder("");
            var path = Path.Combine(AppContext.BaseDirectory, "CustomSql", "missing.sql");

            Assert.Throws<FileNotFoundException>(() => migrationBuilder.SqlFile(path));
        }
    }
}
