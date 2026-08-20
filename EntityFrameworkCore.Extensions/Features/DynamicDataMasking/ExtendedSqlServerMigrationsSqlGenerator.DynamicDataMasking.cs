using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Extensions.Services;

internal sealed partial class ExtendedSqlServerMigrationsSqlGenerator
{
    /// <inheritdoc />
    protected override void Generate(
        CreateTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        var maskedColumns = new List<(AddColumnOperation Column, string MaskingFunction)>();
        foreach (var column in operation.Columns)
        {
            if (GetMaskingFunction(column) is { } maskingFunction)
            {
                maskedColumns.Add((column, maskingFunction));
            }
        }

        ThrowIfUnterminatedMaskStatementsAreRequired(
            operation,
            terminate,
            maskedColumns.Count > 0);
        base.Generate(operation, model, builder, terminate);

        foreach (var (column, maskingFunction) in maskedColumns)
        {
            GenerateAddMaskingStatement(column, maskingFunction, builder);
        }
    }

    /// <inheritdoc />
    protected override void Generate(
        AddColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        var maskingFunction = GetMaskingFunction(operation);
        ThrowIfUnterminatedMaskStatementsAreRequired(
            operation,
            terminate,
            maskingFunction is not null);

        base.Generate(operation, model, builder, terminate);

        if (maskingFunction is not null)
        {
            GenerateAddMaskingStatement(operation, maskingFunction, builder);
        }
    }

    /// <inheritdoc />
    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        var oldMaskingFunction = GetMaskingFunction(operation.OldColumn);
        var newMaskingFunction = GetMaskingFunction(operation);
        try
        {
            // The extension annotation must not make the base provider emit a physical ALTER COLUMN
            // for a mask-only change. Removing it also prevents nested AddColumnOperation generation
            // from emitting a duplicate mask when SQL Server recreates a column.
            SetMaskingFunction(operation, maskingFunction: null);
            SetMaskingFunction(operation.OldColumn, maskingFunction: null);
            base.Generate(operation, model, builder);
        }
        finally
        {
            SetMaskingFunction(operation, newMaskingFunction);
            SetMaskingFunction(operation.OldColumn, oldMaskingFunction);
        }

        if (newMaskingFunction is not null)
        {
            // ADD MASKED creates, replaces, or restores the target mask, so no knowledge of the
            // base provider's physical-column generation path is required.
            GenerateAddMaskingStatement(operation, newMaskingFunction, builder);
        }
        else if (oldMaskingFunction is not null)
        {
            // A physical ALTER COLUMN may already have removed the mask. Guarding the drop makes
            // both that case and an already-unmasked drifted database safe.
            GenerateDropMaskingStatement(operation, builder);
        }
    }

    private void GenerateAddMaskingStatement(
        ColumnOperation operation,
        string maskingFunction,
        MigrationCommandListBuilder builder)
    {
        AppendAlterColumn(operation, builder);

        var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));
        builder.Append(" ADD MASKED WITH (FUNCTION = ")
            .Append(stringTypeMapping.GenerateSqlLiteral(maskingFunction))
            .Append(")")
            .Append(Dependencies.SqlGenerationHelper.StatementTerminator)
            .EndCommand();
    }

    private void GenerateDropMaskingStatement(
        ColumnOperation operation,
        MigrationCommandListBuilder builder)
    {
        var sqlHelper = Dependencies.SqlGenerationHelper;
        var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));

        // SQL Server validates DROP MASKED while compiling a batch, even when an IF condition is false.
        // Dynamic SQL defers that validation until the catalog check confirms that the mask still exists.
        builder.Append("IF EXISTS (SELECT 1 FROM [sys].[masked_columns] WHERE [object_id] = OBJECT_ID(")
            .Append(stringTypeMapping.GenerateSqlLiteral(
                sqlHelper.DelimitIdentifier(operation.Table, operation.Schema)))
            .Append(") AND [name] = ")
            .Append(stringTypeMapping.GenerateSqlLiteral(operation.Name))
            .Append(" AND [is_masked] = 1)")
            .AppendLine()
            .AppendLine("BEGIN")
            .Append("    EXEC(")
            .Append(stringTypeMapping.GenerateSqlLiteral(
                $"ALTER TABLE {sqlHelper.DelimitIdentifier(operation.Table, operation.Schema)} " +
                $"ALTER COLUMN {sqlHelper.DelimitIdentifier(operation.Name)} DROP MASKED" +
                sqlHelper.StatementTerminator))
            .AppendLine(");")
            .Append("END")
            .Append(sqlHelper.StatementTerminator)
            .EndCommand();
    }

    private void AppendAlterColumn(ColumnOperation operation, MigrationCommandListBuilder builder)
    {
        var sqlHelper = Dependencies.SqlGenerationHelper;
        builder.Append("ALTER TABLE ")
            .Append(sqlHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" ALTER COLUMN ")
            .Append(sqlHelper.DelimitIdentifier(operation.Name));
    }

    private static string? GetMaskingFunction(ColumnOperation operation)
        => DynamicDataMaskingAnnotation.GetMaskingFunction(
            operation,
            operation.Schema,
            operation.Table,
            operation.Name);

    private static void SetMaskingFunction(ColumnOperation operation, string? maskingFunction)
    {
        var annotatable = (IMutableAnnotatable)operation;
        if (maskingFunction is null)
        {
            annotatable.RemoveAnnotation(AnnotationConstants.DynamicDataMasking);
        }
        else
        {
            annotatable.SetAnnotation(AnnotationConstants.DynamicDataMasking, maskingFunction);
        }
    }

    private static void ThrowIfUnterminatedMaskStatementsAreRequired(
        MigrationOperation operation,
        bool terminate,
        bool maskStatementsRequired)
    {
        if (!terminate && maskStatementsRequired)
        {
            throw new InvalidOperationException(
                $"Cannot generate unterminated SQL for a masked {operation.GetType().Name}; " +
                "applying the mask requires an additional SQL statement.");
        }
    }
}
