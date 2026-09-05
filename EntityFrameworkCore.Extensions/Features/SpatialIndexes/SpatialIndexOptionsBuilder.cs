using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Configures SQL Server spatial index options.
/// </summary>
public sealed class SpatialIndexOptionsBuilder
{
    private readonly IMutableIndex _index;

    internal SpatialIndexOptionsBuilder(IMutableIndex index)
    {
        _index = index;
    }

    /// <summary>
    /// Configures the geometry coordinate space covered by the spatial index.
    /// </summary>
    /// <param name="xMin">The minimum X coordinate.</param>
    /// <param name="yMin">The minimum Y coordinate.</param>
    /// <param name="xMax">The maximum X coordinate.</param>
    /// <param name="yMax">The maximum Y coordinate.</param>
    /// <returns>The same options builder.</returns>
    /// <remarks>A bounding box is required for <c>geometry</c> and is not valid for <c>geography</c>.</remarks>
    public SpatialIndexOptionsBuilder HasBoundingBox(double xMin, double yMin, double xMax, double yMax)
    {
        ThrowIfNotFinite(xMin, nameof(xMin));
        ThrowIfNotFinite(yMin, nameof(yMin));
        ThrowIfNotFinite(xMax, nameof(xMax));
        ThrowIfNotFinite(yMax, nameof(yMax));

        if (xMin >= xMax)
        {
            throw new ArgumentOutOfRangeException(
                nameof(xMin),
                "The minimum X coordinate must be less than the maximum X coordinate.");
        }

        if (yMin >= yMax)
        {
            throw new ArgumentOutOfRangeException(
                nameof(yMin),
                "The minimum Y coordinate must be less than the maximum Y coordinate.");
        }

        _index.SetAnnotation(AnnotationConstants.SpatialIndexBoundingBoxXMin, xMin);
        _index.SetAnnotation(AnnotationConstants.SpatialIndexBoundingBoxYMin, yMin);
        _index.SetAnnotation(AnnotationConstants.SpatialIndexBoundingBoxXMax, xMax);
        _index.SetAnnotation(AnnotationConstants.SpatialIndexBoundingBoxYMax, yMax);

        return this;
    }

    /// <summary>
    /// Configures the maximum number of tessellation cells used for one spatial object.
    /// </summary>
    /// <param name="cellsPerObject">A value from 1 through 8192.</param>
    /// <returns>The same options builder.</returns>
    public SpatialIndexOptionsBuilder HasCellsPerObject(int cellsPerObject)
    {
        if (cellsPerObject is < 1 or > 8192)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellsPerObject),
                cellsPerObject,
                "Cells per object must be between 1 and 8192.");
        }

        _index.SetAnnotation(AnnotationConstants.SpatialIndexCellsPerObject, cellsPerObject);

        return this;
    }

    private static void ThrowIfNotFinite(double coordinate, string parameterName)
    {
        if (!double.IsFinite(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                coordinate,
                "Spatial index bounding-box coordinates must be finite numbers.");
        }
    }
}
