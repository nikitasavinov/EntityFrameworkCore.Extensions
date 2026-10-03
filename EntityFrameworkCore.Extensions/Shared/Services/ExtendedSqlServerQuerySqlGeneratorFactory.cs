using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // The SQL Server query SQL generator depends on provider-internal options.

/// <summary>
/// Creates <see cref="ExtendedSqlServerQuerySqlGenerator" /> instances for SQL Server queries.
/// </summary>
internal sealed class ExtendedSqlServerQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
{
    private readonly QuerySqlGeneratorDependencies _dependencies;
    private readonly IRelationalTypeMappingSource _typeMappingSource;
    private readonly ISqlServerSingletonOptions _sqlServerSingletonOptions;

    /// <summary>Initializes a new factory instance.</summary>
    /// <param name="dependencies">The query SQL generator dependencies.</param>
    /// <param name="typeMappingSource">The relational type mapping source.</param>
    /// <param name="sqlServerSingletonOptions">The SQL Server singleton options.</param>
    public ExtendedSqlServerQuerySqlGeneratorFactory(
        QuerySqlGeneratorDependencies dependencies,
        IRelationalTypeMappingSource typeMappingSource,
        ISqlServerSingletonOptions sqlServerSingletonOptions)
    {
        _dependencies = dependencies;
        _typeMappingSource = typeMappingSource;
        _sqlServerSingletonOptions = sqlServerSingletonOptions;
    }

    /// <inheritdoc />
    public QuerySqlGenerator Create()
        => new ExtendedSqlServerQuerySqlGenerator(_dependencies, _typeMappingSource, _sqlServerSingletonOptions);
}

#pragma warning restore EF1001
