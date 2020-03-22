using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.IO;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests
{
    public class MigrationBuilderExtensionsTest
    {
        [Fact]
        public void SqlFile_should_include_sql_in_migration()
        {
            var migrationBuilder = new MigrationBuilder("");

            migrationBuilder.SqlFile(Path.Combine("CustomSql", "TestSql.sql"));

            Assert.Single(migrationBuilder.Operations);
            Assert.IsType<SqlOperation>(migrationBuilder.Operations[0]);
            Assert.Equal("PRINT 'test1234'", ((SqlOperation)migrationBuilder.Operations[0]).Sql);
        }
    }
}
