using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Preserving update tags depends on SQL Server's parameter SQL processor.

/// <summary>
/// SQL Server parameter SQL processor that keeps query tags on <see cref="UpdateExpression" />.
/// </summary>
internal sealed class ExtendedSqlServerParameterBasedSqlProcessor : SqlServerParameterBasedSqlProcessor
{
    /// <summary>Initializes a new processor instance.</summary>
    /// <param name="dependencies">The parameter SQL processor dependencies.</param>
    /// <param name="parameters">The parameter SQL processor parameters.</param>
    /// <param name="sqlServerSingletonOptions">The SQL Server singleton options.</param>
    public ExtendedSqlServerParameterBasedSqlProcessor(
        RelationalParameterBasedSqlProcessorDependencies dependencies,
        RelationalParameterBasedSqlProcessorParameters parameters,
        ISqlServerSingletonOptions sqlServerSingletonOptions)
        : base(dependencies, parameters, sqlServerSingletonOptions)
    {
    }

    /// <inheritdoc />
    public override Expression Process(Expression queryExpression, ParametersCacheDecorator parametersDecorator)
    {
        // EF Core 10 rebuilds an UpdateExpression with an empty tag set whenever a visitor changes a child,
        // which parameter expansion does. DeleteExpression keeps its tags. Capture them and put them back.
        var updateTags = (queryExpression as UpdateExpression)?.Tags;
        var processed = base.Process(queryExpression, parametersDecorator);
        if (updateTags is not { Count: > 0 }
            || processed is not UpdateExpression update
            || update.Tags.Count > 0)
        {
            return processed;
        }

        return update.ApplyTags(updateTags);
    }
}

#pragma warning restore EF1001
