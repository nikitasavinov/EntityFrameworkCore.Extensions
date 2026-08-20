using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Generates SQL Server migration commands for EntityFrameworkCore.Extensions annotations.
/// </summary>
internal sealed partial class ExtendedSqlServerMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
{
    /// <summary>Initializes a new generator instance.</summary>
    /// <param name="dependencies">The relational migration SQL dependencies.</param>
    /// <param name="commandBatchPreparer">The SQL Server modification-command batch preparer.</param>
    public ExtendedSqlServerMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer)
        : base(dependencies, commandBatchPreparer)
    {
    }
}
