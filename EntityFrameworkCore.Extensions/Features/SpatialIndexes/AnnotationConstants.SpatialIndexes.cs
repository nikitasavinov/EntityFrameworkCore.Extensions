namespace EntityFrameworkCore.Extensions;

public static partial class AnnotationConstants
{
    /// <summary>
    /// Identifies a SQL Server spatial index.
    /// </summary>
    public const string SpatialIndex = "EntityFrameworkCore.Extensions:SpatialIndex";

    /// <summary>
    /// Identifies the SQL Server spatial type used by a spatial index.
    /// </summary>
    public const string SpatialIndexType = "EntityFrameworkCore.Extensions:SpatialIndexType";

    /// <summary>
    /// Identifies the minimum X coordinate of a geometry spatial index bounding box.
    /// </summary>
    public const string SpatialIndexBoundingBoxXMin = "EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxXMin";

    /// <summary>
    /// Identifies the minimum Y coordinate of a geometry spatial index bounding box.
    /// </summary>
    public const string SpatialIndexBoundingBoxYMin = "EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxYMin";

    /// <summary>
    /// Identifies the maximum X coordinate of a geometry spatial index bounding box.
    /// </summary>
    public const string SpatialIndexBoundingBoxXMax = "EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxXMax";

    /// <summary>
    /// Identifies the maximum Y coordinate of a geometry spatial index bounding box.
    /// </summary>
    public const string SpatialIndexBoundingBoxYMax = "EntityFrameworkCore.Extensions:SpatialIndexBoundingBoxYMax";

    /// <summary>
    /// Identifies the cells-per-object setting of a SQL Server spatial index.
    /// </summary>
    public const string SpatialIndexCellsPerObject = "EntityFrameworkCore.Extensions:SpatialIndexCellsPerObject";
}
