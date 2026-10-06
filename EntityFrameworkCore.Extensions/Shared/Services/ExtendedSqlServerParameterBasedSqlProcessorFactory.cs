using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // The SQL Server parameter SQL processor depends on provider-internal options.

/// <summary>
/// Creates <see cref="ExtendedSqlServerParameterBasedSqlProcessor" /> instances.
/// </summary>
internal sealed class ExtendedSqlServerParameterBasedSqlProcessorFactory : IRelationalParameterBasedSqlProcessorFactory
{
    private readonly RelationalParameterBasedSqlProcessorDependencies _dependencies;
    private readonly ISqlServerSingletonOptions _sqlServerSingletonOptions;

    /// <summary>Initializes a new factory instance.</summary>
    /// <param name="dependencies">The parameter SQL processor dependencies.</param>
    /// <param name="sqlServerSingletonOptions">The SQL Server singleton options.</param>
    public ExtendedSqlServerParameterBasedSqlProcessorFactory(
        RelationalParameterBasedSqlProcessorDependencies dependencies,
        ISqlServerSingletonOptions sqlServerSingletonOptions)
    {
        _dependencies = dependencies;
        _sqlServerSingletonOptions = sqlServerSingletonOptions;
    }

    /// <inheritdoc />
    public RelationalParameterBasedSqlProcessor Create(RelationalParameterBasedSqlProcessorParameters parameters)
        => new ExtendedSqlServerParameterBasedSqlProcessor(_dependencies, parameters, _sqlServerSingletonOptions);
}

#pragma warning restore EF1001
