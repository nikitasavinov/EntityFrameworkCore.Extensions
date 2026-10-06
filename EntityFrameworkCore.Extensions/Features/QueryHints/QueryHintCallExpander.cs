using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Rewrites <see cref="QueryableExtensions.WithQueryHints{T}(IQueryable{T}, QueryHint[])" /> and
/// <see cref="QueryableExtensions.WithTableHints{T}(IQueryable{T}, TableHint[])" /> before EF Core translates them.
/// Direct calls already become <c>TagWith</c>. Compiled queries and subqueries leave the call in the tree, and a
/// captured hint would otherwise become a query parameter. Global query filters are added later, during
/// preprocessing, so those calls are registered as tags after the provider preprocessor has run.
/// </summary>
internal static class QueryHintCallExpander
{
    private static readonly MethodInfo QueryHintsMethod = GetHintMethod(nameof(QueryableExtensions.WithQueryHints));
    private static readonly MethodInfo TableHintsMethod = GetHintMethod(nameof(QueryableExtensions.WithTableHints));
    private static readonly MethodInfo TagWithMethod = typeof(EntityFrameworkQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
            method.Name == nameof(EntityFrameworkQueryableExtensions.TagWith)
            && method.IsGenericMethodDefinition
            && method.GetParameters() is [_, { ParameterType: var parameterType }]
            && parameterType == typeof(string));

    /// <summary>
    /// Rewrites hint calls to <c>TagWith</c> so the query normalizer records them before parameter extraction.
    /// </summary>
    public static Expression Expand(Expression expression)
        => new Visitor(queryCompilationContext: null).Visit(expression);

    /// <summary>
    /// Registers hint calls that appear only after preprocessing, such as calls inside a global query filter.
    /// The query normalizer has already run, so the call is removed and the tag is added directly.
    /// </summary>
    public static Expression ExpandRemaining(Expression expression, QueryCompilationContext queryCompilationContext)
        => new Visitor(queryCompilationContext).Visit(expression);

    private static MethodInfo GetHintMethod(string name)
        => typeof(QueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == name && method.IsGenericMethodDefinition);

    private sealed class Visitor(QueryCompilationContext? queryCompilationContext) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var visited = base.VisitMethodCall(node);
            if (visited is not MethodCallExpression methodCall
                || !methodCall.Method.IsGenericMethod)
            {
                return visited;
            }

            var definition = methodCall.Method.GetGenericMethodDefinition();
            if (definition != QueryHintsMethod && definition != TableHintsMethod)
            {
                return visited;
            }

            var hints = methodCall.Arguments[1];
            if (ContainsParameter(hints))
            {
                throw new InvalidOperationException(
                    $"{methodCall.Method.Name} must be called with hints that do not depend on query parameters.");
            }

            var tag = definition == QueryHintsMethod
                ? QueryableExtensions.CreateQueryHintTag(Evaluate<QueryHint[]>(hints))
                : QueryableExtensions.CreateTableHintTag(Evaluate<TableHint[]>(hints));
            if (queryCompilationContext is not null)
            {
                queryCompilationContext.AddTag(tag);
                return methodCall.Arguments[0];
            }

            var tagWith = TagWithMethod.MakeGenericMethod(methodCall.Method.GetGenericArguments());
            return Expression.Call(tagWith, methodCall.Arguments[0], Expression.Constant(tag));
        }

        private static T Evaluate<T>(Expression expression)
        {
            if (expression is ConstantExpression { Value: T value })
            {
                return value;
            }

            // Interpretation avoids compiling a new delegate on every subquery execution.
            return Expression.Lambda<Func<T>>(expression).Compile(preferInterpretation: true)();
        }

        private static bool ContainsParameter(Expression expression)
        {
            var finder = new ParameterFinder();
            finder.Visit(expression);
            return finder.Found;
        }
    }

    private sealed class ParameterFinder : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _bound = [];

        public bool Found { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            foreach (var parameter in node.Parameters)
            {
                _bound.Add(parameter);
            }

            try
            {
                return base.VisitLambda(node);
            }
            finally
            {
                foreach (var parameter in node.Parameters)
                {
                    _bound.Remove(parameter);
                }
            }
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            foreach (var variable in node.Variables)
            {
                _bound.Add(variable);
            }

            try
            {
                return base.VisitBlock(node);
            }
            finally
            {
                foreach (var variable in node.Variables)
                {
                    _bound.Remove(variable);
                }
            }
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_bound.Contains(node))
            {
                Found = true;
            }

            return node;
        }

        protected override Expression VisitExtension(Expression node)
        {
            // Compiled-query arguments are already QueryParameterExpression nodes by the time a query is compiled.
            if (node.GetType().Name == "QueryParameterExpression")
            {
                Found = true;
            }

            return base.VisitExtension(node);
        }
    }
}
