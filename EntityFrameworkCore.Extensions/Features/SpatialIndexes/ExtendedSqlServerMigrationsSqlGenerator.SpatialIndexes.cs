using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Extensions.Services;

internal sealed partial class ExtendedSqlServerMigrationsSqlGenerator
{
    private bool TryGenerateSpatialIndex(
        CreateIndexOperation operation,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        var indexName = operation.Schema is null
            ? $"{operation.Table}.{operation.Name}"
            : $"{operation.Schema}.{operation.Table}.{operation.Name}";
        var configuration = SpatialIndexAnnotation.GetConfiguration(operation, indexName);
        if (configuration is null)
        {
            return false;
        }

        SpatialIndexAnnotation.ValidateIndex(
            operation, indexName, operation.Columns.Length, operation.IsUnique, operation.Filter, operation.IsDescending);

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

        return true;
    }

    private static string FormatCoordinate(double coordinate)
        => coordinate.ToString("R", CultureInfo.InvariantCulture);
}
