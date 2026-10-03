using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's query SQL generator requires its provider-internal implementation.

/// <summary>
/// Generates SQL Server query SQL for EntityFrameworkCore.Extensions query features.
/// </summary>
internal sealed partial class ExtendedSqlServerQuerySqlGenerator : SqlServerQuerySqlGenerator
{
    /// <summary>Initializes a new generator instance.</summary>
    /// <param name="dependencies">The query SQL generator dependencies.</param>
    /// <param name="typeMappingSource">The relational type mapping source.</param>
    /// <param name="sqlServerSingletonOptions">The SQL Server singleton options.</param>
    public ExtendedSqlServerQuerySqlGenerator(
        QuerySqlGeneratorDependencies dependencies,
        IRelationalTypeMappingSource typeMappingSource,
        ISqlServerSingletonOptions sqlServerSingletonOptions)
        : base(dependencies, typeMappingSource, sqlServerSingletonOptions)
    {
    }
}

#pragma warning restore EF1001
