using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Defers provider compatibility validation until all context options have been configured.
/// </summary>
internal sealed class EntityFrameworkCoreExtensionsOptionsExtension : IDbContextOptionsExtension
{
    private readonly DbContextOptionsExtensionInfo _info;

    public EntityFrameworkCoreExtensionsOptionsExtension()
    {
        _info = new ExtensionInfo(this);
    }

    public DbContextOptionsExtensionInfo Info => _info;

    void IDbContextOptionsExtension.ApplyServices(IServiceCollection services)
    {
    }

    IDbContextOptionsExtension IDbContextOptionsExtension.ApplyDefaults(IDbContextOptions options) => this;

    void IDbContextOptionsExtension.Validate(IDbContextOptions options)
    {
        var providerExtensions = options.Extensions
            .Where(extension => extension.Info.IsDatabaseProvider)
            .ToList();
        if (providerExtensions.Count == 0)
        {
            throw new InvalidOperationException(
                "Configure a database provider before using EntityFrameworkCore.Extensions.");
        }

        if (providerExtensions.Count > 1)
        {
            throw new InvalidOperationException(
                "Only one database provider can be configured for a DbContext.");
        }

        var providerExtension = providerExtensions[0];
        if (providerExtension is RelationalOptionsExtension
            && providerExtension.GetType().Assembly != typeof(SqlServerDbContextOptionsExtensions).Assembly)
        {
            throw new InvalidOperationException(
                "EntityFrameworkCore.Extensions supports only SQL Server when a relational provider is configured.");
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => string.Empty;

        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["EntityFrameworkCoreExtensions:Enabled"] = "1";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;
    }
}
