using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's annotation provider requires its provider-internal implementation.

/// <summary>
/// Propagates EntityFrameworkCore.Extensions annotations to the SQL Server relational model.
/// </summary>
internal sealed partial class ExtendedSqlServerAnnotationProvider : SqlServerAnnotationProvider
{
    /// <summary>Initializes a new annotation provider instance.</summary>
    /// <param name="dependencies">The relational annotation provider dependencies.</param>
    public ExtendedSqlServerAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : base(dependencies)
    {
    }
}

#pragma warning restore EF1001
