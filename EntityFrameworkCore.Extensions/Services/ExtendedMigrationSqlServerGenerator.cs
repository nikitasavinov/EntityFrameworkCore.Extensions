using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Extensions.Services
{
    public class ExtendedMigrationSqlServerGenerator : SqlServerMigrationsSqlGenerator
    {
        public ExtendedMigrationSqlServerGenerator(MigrationsSqlGeneratorDependencies dependencies, IMigrationsAnnotationProvider migrationsAnnotations) : base(dependencies, migrationsAnnotations)
        {
        }

        protected override void Generate(CreateTableOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate)
        {
            base.Generate(operation, model, builder);

            foreach (var columnOperation in operation.Columns)
            {
                AddMaskingFunction(columnOperation, builder);
            }
        }

        protected override void Generate(AddColumnOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate)
        {
            base.Generate(operation, model, builder, terminate);

            AddMaskingFunction(operation, builder);
        }

        protected override void Generate(AlterColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            base.Generate(operation, model, builder);
            if (ColumnAnnotationAdded(AnnotationConstants.DynamicDataMasking, operation.OldColumn, operation))
            {
                AddMaskingFunction(operation, builder);
            }
            if (ColumnAnnotationRemoved(AnnotationConstants.DynamicDataMasking, operation.OldColumn, operation))
            {
                DropMaskingFunction(operation, builder);
            }
        }

        private void AddMaskingFunction(AlterColumnOperation operation, MigrationCommandListBuilder builder)
        {
            var sqlHelper = Dependencies.SqlGenerationHelper;
            var addDynamicMask = operation.FindAnnotation(AnnotationConstants.DynamicDataMasking);

            builder.Append("ALTER TABLE ")
                .Append(sqlHelper.DelimitIdentifier(operation.Table, operation.Schema))
                .Append($" ALTER COLUMN {operation.Name}")
                .Append($" ADD MASKED WITH (FUNCTION='{addDynamicMask.Value}')")
                .Append(sqlHelper.StatementTerminator)
                .EndCommand();
        }

        private void AddMaskingFunction(AddColumnOperation column, MigrationCommandListBuilder builder)
        {
            var sqlHelper = Dependencies.SqlGenerationHelper;

            var addDynamicMask = column.FindAnnotation(AnnotationConstants.DynamicDataMasking);
            if (addDynamicMask != null)
            {
                builder.Append("ALTER TABLE ")
                    .Append(sqlHelper.DelimitIdentifier(column.Table, column.Schema))
                    .Append($" ALTER COLUMN {column.Name}")
                    .Append($" ADD MASKED WITH (FUNCTION='{addDynamicMask.Value}')")
                    .Append(sqlHelper.StatementTerminator)
                    .EndCommand();
            }
        }

        private void DropMaskingFunction(AlterColumnOperation operation, MigrationCommandListBuilder builder)
        {
            builder.Append("ALTER TABLE ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
                .Append($" ALTER COLUMN {operation.Name}")
                .Append($" DROP MASKED")
                .EndCommand();
        }

        private bool ColumnAnnotationAdded(string annotrationName, ColumnOperation oldColumn, ColumnOperation newColumn) => oldColumn.FindAnnotation(annotrationName) == null && newColumn.FindAnnotation(annotrationName) != null;

        private bool ColumnAnnotationRemoved(string annotrationName, ColumnOperation oldColumn, ColumnOperation newColumn) => !ColumnAnnotationAdded(annotrationName, oldColumn, newColumn);
    }
}
