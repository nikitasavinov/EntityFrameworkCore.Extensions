using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Rewriting hint calls before parameterization depends on EF Core's query compiler.
#pragma warning disable EF9100 // PrecompileQuery is the precompilation entry point and must expand hints too.

/// <summary>
/// Expands hint calls before EF Core extracts query parameters, then uses the normal compiler.
/// </summary>
internal sealed class ExtendedQueryCompiler : QueryCompiler
{
    /// <summary>Initializes a new compiler instance.</summary>
    /// <param name="queryContextFactory">The query context factory.</param>
    /// <param name="compiledQueryCache">The compiled query cache.</param>
    /// <param name="compiledQueryCacheKeyGenerator">The compiled query cache key generator.</param>
    /// <param name="database">The database.</param>
    /// <param name="logger">The query logger.</param>
    /// <param name="currentContext">The current context.</param>
    /// <param name="evaluatableExpressionFilter">The evaluatable expression filter.</param>
    /// <param name="model">The model.</param>
    public ExtendedQueryCompiler(
        IQueryContextFactory queryContextFactory,
        ICompiledQueryCache compiledQueryCache,
        ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
        IDatabase database,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger,
        ICurrentDbContext currentContext,
        IEvaluatableExpressionFilter evaluatableExpressionFilter,
        IModel model)
        : base(
            queryContextFactory,
            compiledQueryCache,
            compiledQueryCacheKeyGenerator,
            database,
            logger,
            currentContext,
            evaluatableExpressionFilter,
            model)
    {
    }

    /// <inheritdoc />
    public override Expression ExtractParameters(
        Expression query,
        Dictionary<string, object?> parameters,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger,
        bool compiledQuery = false,
        bool generateContextAccessors = false)
        => base.ExtractParameters(
            QueryHintCallExpander.Expand(query),
            parameters,
            logger,
            compiledQuery,
            generateContextAccessors);

    /// <inheritdoc />
    public override Expression<Func<QueryContext, TResult>> PrecompileQuery<TResult>(Expression query, bool async)
        => base.PrecompileQuery<TResult>(QueryHintCallExpander.Expand(query), async);
}

#pragma warning restore EF9100
#pragma warning restore EF1001
