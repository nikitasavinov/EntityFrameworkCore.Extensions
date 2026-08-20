using System.Globalization;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Extensions.Services;

internal sealed partial class ExtendedSqlServerMigrationsSqlGenerator
{
    private static readonly string[] UnsupportedSpatialIndexAnnotations =
    [
        "SqlServer:FillFactor",
        "SqlServer:SortInTempDb",
        "SqlServer:DataCompression",
    ];

    /// <inheritdoc />
    protected override void Generate(
        CreateIndexOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        var indexName = operation.Schema is null
            ? $"{operation.Table}.{operation.Name}"
            : $"{operation.Schema}.{operation.Table}.{operation.Name}";
        var configuration = SpatialIndexAnnotation.GetConfiguration(operation, indexName);
        if (configuration is null)
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        ValidateSpatialIndexOperation(operation, indexName);

        var sqlHelper = Dependencies.SqlGenerationHelper;
        builder.Append("CREATE SPATIAL INDEX ")
            .Append(sqlHelper.DelimitIdentifier(operation.Name))
            .Append(" ON ")
            .Append(sqlHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" (")
            .Append(sqlHelper.DelimitIdentifier(operation.Columns[0]))
            .AppendLine(")")
            .Append("USING ")
            .Append(configuration.SpatialType == SpatialIndexAnnotation.Geography
                ? "GEOGRAPHY_AUTO_GRID"
                : "GEOMETRY_AUTO_GRID");

        if (configuration.BoundingBox is not null || configuration.CellsPerObject is not null)
        {
            builder.AppendLine()
                .Append("WITH (");

            if (configuration.BoundingBox is { } boundingBox)
            {
                builder.Append("BOUNDING_BOX = (")
                    .Append(FormatCoordinate(boundingBox.XMin))
                    .Append(", ")
                    .Append(FormatCoordinate(boundingBox.YMin))
                    .Append(", ")
                    .Append(FormatCoordinate(boundingBox.XMax))
                    .Append(", ")
                    .Append(FormatCoordinate(boundingBox.YMax))
                    .Append(")");
            }

            if (configuration.CellsPerObject is { } cellsPerObject)
            {
                if (configuration.BoundingBox is not null)
                {
                    builder.Append(", ");
                }

                builder.Append("CELLS_PER_OBJECT = ")
                    .Append(cellsPerObject.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(")");
        }

        if (terminate)
        {
            builder.Append(sqlHelper.StatementTerminator)
                .EndCommand();
        }
    }

    private static void ValidateSpatialIndexOperation(CreateIndexOperation operation, string indexName)
    {
        if (operation.Columns.Length != 1)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' must target exactly one column.");
        }

        if (operation.IsUnique)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot be unique.");
        }

        if (operation.Filter is not null)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot have a filter.");
        }

        if (operation.IsDescending is not null)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot specify sort order.");
        }

        if (operation["SqlServer:Clustered"] is true)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot be clustered.");
        }

        if (operation["SqlServer:Include"] is Array { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' cannot have included columns.");
        }

        if (operation.FindAnnotation("SqlServer:Online") is not null)
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' does not support ONLINE.");
        }

        if (UnsupportedSpatialIndexAnnotations.Any(name => operation.FindAnnotation(name) is not null))
        {
            throw new InvalidOperationException(
                $"Spatial index '{indexName}' does not support additional SQL Server index options yet.");
        }
    }

    private static string FormatCoordinate(double coordinate)
        => coordinate.ToString("R", CultureInfo.InvariantCulture);
}
