using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Wraps the database provider's query preprocessor and registers hint calls that the provider adds while
/// preprocessing, including calls inside global query filters.
/// </summary>
internal sealed class ExtendedQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
{
    private readonly QueryTranslationPreprocessorDependencies _dependencies;
    private readonly IQueryTranslationPreprocessorFactory _providerFactory;

    /// <summary>Initializes a new factory instance.</summary>
    /// <param name="dependencies">The query translation preprocessor dependencies.</param>
    /// <param name="options">The context options, used to find the provider preprocessor.</param>
    /// <param name="serviceProvider">The context service provider, used to construct the provider preprocessor.</param>
    public ExtendedQueryTranslationPreprocessorFactory(
        QueryTranslationPreprocessorDependencies dependencies,
        IDbContextOptions options,
        IServiceProvider serviceProvider)
    {
        _dependencies = dependencies;
        _providerFactory = CreateProviderFactory(options, serviceProvider);
    }

    /// <summary>The preprocessor factory registered by the active database provider.</summary>
    internal IQueryTranslationPreprocessorFactory ProviderFactory => _providerFactory;

    /// <inheritdoc />
    public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        => new QueryHintExpandingQueryTranslationPreprocessor(
            _providerFactory.Create(queryCompilationContext),
            _dependencies,
            queryCompilationContext);

    private static IQueryTranslationPreprocessorFactory CreateProviderFactory(
        IDbContextOptions options,
        IServiceProvider serviceProvider)
    {
        // ReplaceService drops the provider registration. Replaying the other extensions finds the factory the
        // provider would have registered, so SQL Server, InMemory, and any other provider keep their own preprocessor.
        var services = new DescriptorList();
        foreach (var extension in options.Extensions)
        {
            if (extension is EntityFrameworkCoreExtensionsOptionsExtension)
            {
                continue;
            }

            extension.ApplyServices(services);
        }

        var implementationType = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IQueryTranslationPreprocessorFactory))
            ?.ImplementationType
            ?? throw new InvalidOperationException(
                "The database provider did not register a query translation preprocessor.");

        return (IQueryTranslationPreprocessorFactory)ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
    }

    private sealed class DescriptorList : IServiceCollection
    {
        private readonly List<ServiceDescriptor> _items = [];

        public ServiceDescriptor this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(ServiceDescriptor item) => _items.Add(item);

        public void Clear() => _items.Clear();

        public bool Contains(ServiceDescriptor item) => _items.Contains(item);

        public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<ServiceDescriptor> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(ServiceDescriptor item) => _items.IndexOf(item);

        public void Insert(int index, ServiceDescriptor item) => _items.Insert(index, item);

        public bool Remove(ServiceDescriptor item) => _items.Remove(item);

        public void RemoveAt(int index) => _items.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// Runs the provider preprocessor, then registers hint calls that preprocessing introduced.
/// </summary>
internal sealed class QueryHintExpandingQueryTranslationPreprocessor : QueryTranslationPreprocessor
{
    private readonly QueryTranslationPreprocessor _providerPreprocessor;

    public QueryHintExpandingQueryTranslationPreprocessor(
        QueryTranslationPreprocessor providerPreprocessor,
        QueryTranslationPreprocessorDependencies dependencies,
        QueryCompilationContext queryCompilationContext)
        : base(dependencies, queryCompilationContext)
    {
        _providerPreprocessor = providerPreprocessor;
    }

    /// <inheritdoc />
    public override Expression Process(Expression query)
        => QueryHintCallExpander.ExpandRemaining(_providerPreprocessor.Process(query), QueryCompilationContext);
}
