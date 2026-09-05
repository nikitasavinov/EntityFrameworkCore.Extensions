using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.Extensions.Services;

internal static class SpatialIndexAnnotation
{
    public const string Geography = "geography";
    public const string Geometry = "geometry";

    public static SpatialIndexOptions? GetOptions(IReadOnlyAnnotatable annotatable, string objectName)
    {
        var marker = annotatable.FindAnnotation(AnnotationConstants.SpatialIndex);
        if (marker is null)
        {
            return null;
        }

        if (marker.Value is not bool isSpatialIndex)
        {
            throw InvalidAnnotationValue(marker, objectName, "a Boolean");
        }

        if (!isSpatialIndex)
        {
            return null;
        }

        var xMin = GetOptionalValue<double>(
            annotatable,
            AnnotationConstants.SpatialIndexBoundingBoxXMin,
            objectName);
        var yMin = GetOptionalValue<double>(
            annotatable,
            AnnotationConstants.SpatialIndexBoundingBoxYMin,
            objectName);
        var xMax = GetOptionalValue<double>(
            annotatable,
            AnnotationConstants.SpatialIndexBoundingBoxXMax,
            objectName);
        var yMax = GetOptionalValue<double>(
            annotatable,
            AnnotationConstants.SpatialIndexBoundingBoxYMax,
            objectName);

        var specifiedCoordinates = new double?[] { xMin, yMin, xMax, yMax }.Count(value => value.HasValue);
        if (specifiedCoordinates is > 0 and < 4)
        {
            throw new InvalidOperationException(
                $"Spatial index '{objectName}' must specify all four bounding-box coordinates.");
        }

        SpatialBoundingBox? boundingBox = null;
        if (specifiedCoordinates == 4)
        {
            if (!double.IsFinite(xMin!.Value)
                || !double.IsFinite(yMin!.Value)
                || !double.IsFinite(xMax!.Value)
                || !double.IsFinite(yMax!.Value)
                || xMin.Value >= xMax.Value
                || yMin.Value >= yMax.Value)
            {
                throw new InvalidOperationException(
                    $"Spatial index '{objectName}' has an invalid bounding box. Coordinates must be finite, " +
                    "and each minimum must be less than its maximum.");
            }

            boundingBox = new SpatialBoundingBox(xMin.Value, yMin.Value, xMax.Value, yMax.Value);
        }

        var cellsPerObject = GetOptionalValue<int>(
            annotatable,
            AnnotationConstants.SpatialIndexCellsPerObject,
            objectName);
        if (cellsPerObject is < 1 or > 8192)
        {
            throw new InvalidOperationException(
                $"Spatial index '{objectName}' has an invalid cells-per-object value. " +
                "The value must be between 1 and 8192.");
        }

        return new SpatialIndexOptions(boundingBox, cellsPerObject);
    }

    public static SpatialIndexConfiguration? GetConfiguration(
        IReadOnlyAnnotatable annotatable,
        string objectName)
    {
        var options = GetOptions(annotatable, objectName);
        if (options is null)
        {
            return null;
        }

        var spatialTypeAnnotation = annotatable.FindAnnotation(AnnotationConstants.SpatialIndexType)
            ?? throw new InvalidOperationException(
                $"Spatial index '{objectName}' does not identify its SQL Server spatial type.");
        if (spatialTypeAnnotation.Value is not string spatialType
            || (spatialType != Geography && spatialType != Geometry))
        {
            throw InvalidAnnotationValue(
                spatialTypeAnnotation,
                objectName,
                $"either '{Geography}' or '{Geometry}'");
        }

        ValidateOptionsForType(options, spatialType, objectName);
        return new SpatialIndexConfiguration(spatialType, options.BoundingBox, options.CellsPerObject);
    }

    public static void ValidateOptionsForType(
        SpatialIndexOptions options,
        string spatialType,
        string objectName)
    {
        if (spatialType == Geometry && options.BoundingBox is null)
        {
            throw new InvalidOperationException(
                $"Geometry spatial index '{objectName}' requires a bounding box. " +
                $"Configure it with {nameof(SpatialIndexOptionsBuilder)}.{nameof(SpatialIndexOptionsBuilder.HasBoundingBox)}().");
        }

        if (spatialType == Geography && options.BoundingBox is not null)
        {
            throw new InvalidOperationException(
                $"Geography spatial index '{objectName}' cannot have a bounding box.");
        }
    }

    private static T? GetOptionalValue<T>(
        IReadOnlyAnnotatable annotatable,
        string annotationName,
        string objectName)
        where T : struct
    {
        var annotation = annotatable.FindAnnotation(annotationName);
        if (annotation is null)
        {
            return null;
        }

        if (annotation.Value is not T value)
        {
            throw InvalidAnnotationValue(annotation, objectName, $"a {typeof(T).Name}");
        }

        return value;
    }

    private static InvalidOperationException InvalidAnnotationValue(
        IAnnotation annotation,
        string objectName,
        string expectedValue)
        => new(
            $"The '{annotation.Name}' annotation on spatial index '{objectName}' must contain {expectedValue}; " +
            $"found '{annotation.Value?.GetType().Name ?? "null"}'.");
}

internal sealed record SpatialIndexOptions(SpatialBoundingBox? BoundingBox, int? CellsPerObject);

internal sealed record SpatialIndexConfiguration(
    string SpatialType,
    SpatialBoundingBox? BoundingBox,
    int? CellsPerObject);

internal sealed record SpatialBoundingBox(double XMin, double YMin, double XMax, double YMax);
