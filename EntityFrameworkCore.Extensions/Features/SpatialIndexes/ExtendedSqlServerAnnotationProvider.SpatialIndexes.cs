using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's annotation provider requires its provider-internal implementation.

internal sealed partial class ExtendedSqlServerAnnotationProvider
{
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

        var indexName = FormatIndexName(index.Table.Schema, index.Table.Name, index.Name);
        var mappedIndexes = index.MappedIndexes.ToList();
        var spatialIndexes = mappedIndexes
            .Select(mappedIndex => new
            {
                Index = mappedIndex,
                Options = SpatialIndexAnnotation.GetOptions(mappedIndex, indexName),
            })
            .Where(item => item.Options is not null)
            .ToList();

        if (spatialIndexes.Count == 0)
        {
            yield break;
        }

        if (spatialIndexes.Count != mappedIndexes.Count)
        {
            throw new InvalidOperationException(
                $"Relational index '{indexName}' combines spatial and ordinary model indexes.");
        }

        var options = spatialIndexes[0].Options!;
        if (spatialIndexes.Any(item => item.Options != options))
        {
            throw new InvalidOperationException(
                $"Relational index '{indexName}' has conflicting spatial index options.");
        }

        ValidateSpatialIndex(index, spatialIndexes.Select(item => item.Index), indexName);

        var spatialType = index.Columns[0].StoreType.Trim().ToLowerInvariant();
        if (spatialType is not (SpatialIndexAnnotation.Geography or SpatialIndexAnnotation.Geometry))
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' targets column '{index.Columns[0].Name}' with store type " +
                $"'{index.Columns[0].StoreType}'. Only SQL Server geography and geometry columns are supported.");
        }

        SpatialIndexAnnotation.ValidateOptionsForType(options, spatialType, indexName);

        yield return new Annotation(AnnotationConstants.SpatialIndex, true);
        yield return new Annotation(AnnotationConstants.SpatialIndexType, spatialType);
        if (options.BoundingBox is { } boundingBox)
        {
            yield return new Annotation(AnnotationConstants.SpatialIndexBoundingBoxXMin, boundingBox.XMin);
            yield return new Annotation(AnnotationConstants.SpatialIndexBoundingBoxYMin, boundingBox.YMin);
            yield return new Annotation(AnnotationConstants.SpatialIndexBoundingBoxXMax, boundingBox.XMax);
            yield return new Annotation(AnnotationConstants.SpatialIndexBoundingBoxYMax, boundingBox.YMax);
        }

        if (options.CellsPerObject is { } cellsPerObject)
        {
            yield return new Annotation(AnnotationConstants.SpatialIndexCellsPerObject, cellsPerObject);
        }
    }

    private static void ValidateSpatialIndex(
        ITableIndex index,
        IEnumerable<IIndex> mappedIndexes,
        string indexName)
    {
        if (index.Columns.Count != 1)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' must target exactly one column.");
        }

        if (index.IsUnique)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot be unique.");
        }

        if (index.Filter is not null)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot have a filter.");
        }

        if (index.IsDescending is not null)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot specify sort order.");
        }

        foreach (var mappedIndex in mappedIndexes)
        {
            if (mappedIndex.IsClustered() == true)
            {
                throw new InvalidOperationException(
                    $"Spatial index '{indexName}' cannot be clustered.");
            }

            if (mappedIndex.GetIncludeProperties() is { Count: > 0 })
            {
                throw new InvalidOperationException(
                    $"Spatial index '{indexName}' cannot have included columns.");
            }

            if (mappedIndex.IsCreatedOnline() is not null)
            {
                throw new InvalidOperationException(
                    $"Spatial index '{indexName}' does not support ONLINE.");
            }

            if (mappedIndex.GetFillFactor() is not null
                || mappedIndex.GetSortInTempDb() is not null
                || mappedIndex.GetDataCompression() is not null)
            {
                throw new InvalidOperationException(
                    $"Spatial index '{indexName}' does not support additional SQL Server index options yet.");
            }
        }

        var primaryKey = index.Table.PrimaryKey;
        if (primaryKey is null)
        {
            throw new InvalidOperationException(
                $"Table '{index.Table.SchemaQualifiedName}' must have a primary key before spatial index " +
                $"'{index.Name}' can be created.");
        }

        if (primaryKey.MappedKeys.Any(key => key.IsClustered() == false))
        {
            throw new InvalidOperationException(
                $"Table '{index.Table.SchemaQualifiedName}' must have a clustered primary key before spatial index " +
                $"'{index.Name}' can be created.");
        }
    }

    private static string FormatIndexName(string? schema, string table, string index)
        => schema is null ? $"{table}.{index}" : $"{schema}.{table}.{index}";
}

#pragma warning restore EF1001
