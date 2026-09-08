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

    /// <inheritdoc />
    public override IEnumerable<IAnnotation> For(ITableIndex index, bool designTime)
    {
        foreach (var annotation in base.For(index, designTime))
        {
            yield return annotation;
        }

        if (!designTime)
        {
            yield break;
        }

        foreach (var annotation in GetColumnstoreIndexAnnotations(index))
        {
            yield return annotation;
        }

        foreach (var annotation in GetSpatialIndexAnnotations(index))
        {
            yield return annotation;
        }
    }

    private static string FormatIndexName(string? schema, string table, string index)
        => schema is null ? $"{table}.{index}" : $"{schema}.{table}.{index}";
}

#pragma warning restore EF1001
