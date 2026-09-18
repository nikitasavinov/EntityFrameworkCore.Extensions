using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's annotation provider requires its provider-internal implementation.

internal sealed partial class ExtendedSqlServerAnnotationProvider
{
    private static IEnumerable<IAnnotation> GetSpatialIndexAnnotations(ITableIndex index)
    {
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
        foreach (var mappedIndex in mappedIndexes)
        {
            SpatialIndexAnnotation.ValidateIndex(
                mappedIndex, indexName, index.Columns.Count, index.IsUnique, index.Filter, index.IsDescending);
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
}

#pragma warning restore EF1001
