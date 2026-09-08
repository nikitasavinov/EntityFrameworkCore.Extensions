using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Extensions.Services;

internal sealed partial class ExtendedSqlServerMigrationsSqlGenerator
{
    private bool TryGenerateColumnstoreIndex(
        CreateIndexOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        var indexName = operation.Schema is null
            ? $"{operation.Table}.{operation.Name}"
            : $"{operation.Schema}.{operation.Table}.{operation.Name}";
        if (!ColumnstoreIndexAnnotation.IsColumnstoreIndex(operation, indexName))
        {
            return false;
        }

        ColumnstoreIndexAnnotation.ValidateIndex(
            operation, indexName, operation.Columns.Length, operation.IsUnique, operation.IsDescending);

        if (model?.GetRelationalModel().FindTable(operation.Table, operation.Schema) is { } table)
        {
            ColumnstoreIndexAnnotation.ValidateTable(table, operation.Name, indexName);
            foreach (var columnName in operation.Columns)
            {
                if (table.FindColumn(columnName) is { } column)
                {
                    ColumnstoreIndexAnnotation.ValidateColumn(column, indexName);
                }
            }
        }

        // Retain the provider's identifier quoting, filter handling and EXEC wrapping for
        // idempotent scripts, including when it rebuilds indexes during a column alteration.
        base.Generate(operation, model, builder, terminate);
        return true;
    }

    /// <inheritdoc />
    protected override void IndexTraits(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (operation is CreateIndexOperation index
            && ColumnstoreIndexAnnotation.IsColumnstoreIndex(index, index.Name))
        {
            builder.Append("NONCLUSTERED COLUMNSTORE ");
        }
        else
        {
            base.IndexTraits(operation, model, builder);
        }
    }
}
