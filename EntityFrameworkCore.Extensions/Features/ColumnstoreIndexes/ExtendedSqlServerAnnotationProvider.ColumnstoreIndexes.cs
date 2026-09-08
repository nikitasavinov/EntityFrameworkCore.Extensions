using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's annotation provider requires its provider-internal implementation.

internal sealed partial class ExtendedSqlServerAnnotationProvider
{
    private static IEnumerable<IAnnotation> GetColumnstoreIndexAnnotations(ITableIndex index)
    {
        var indexName = FormatIndexName(index.Table.Schema, index.Table.Name, index.Name);
        if (!ColumnstoreIndexAnnotation.IsColumnstoreIndex(index))
        {
            yield break;
        }

        foreach (var mappedIndex in index.MappedIndexes)
        {
            ColumnstoreIndexAnnotation.ValidateIndex(
                mappedIndex, indexName, index.Columns.Count, index.IsUnique, index.IsDescending);
        }

        ColumnstoreIndexAnnotation.ValidateTable(index.Table, index.Name, indexName);

        foreach (var column in index.Columns)
        {
            ColumnstoreIndexAnnotation.ValidateColumn(column, indexName);
        }

        yield return new Annotation(AnnotationConstants.ColumnstoreIndex, true);
    }
}

#pragma warning restore EF1001
