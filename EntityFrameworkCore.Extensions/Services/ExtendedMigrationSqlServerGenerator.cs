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

        protected override void Generate(CreateTableOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            base.Generate(operation, model, builder);

            foreach (var column in operation.Columns)
            {
                var sqlHelper = Dependencies.SqlGenerationHelper;

                var addDynamicMask = column.FindAnnotation(AnnotationConstants.DynamicDataMasking);
                if (addDynamicMask != null)
                {
                    builder.Append("ALTER TABLE ")
                       .Append(sqlHelper.DelimitIdentifier(operation.Name, operation.Schema))
                       .Append($" ALTER COLUMN {column.Name}")
                       .Append($" ADD MASKED WITH (FUNCTION='{addDynamicMask.Value}')")
                       .Append(sqlHelper.StatementTerminator)
                       .EndCommand();
                }
            }
        }

        protected override void Generate(AlterColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            base.Generate(operation, model, builder);
            if (operation.OldColumn.FindAnnotation(AnnotationConstants.DynamicDataMasking) != null && operation.FindAnnotation(AnnotationConstants.DynamicDataMasking) == null)
            {
                builder.Append("ALTER TABLE ")
                   .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
                   .Append($" ALTER COLUMN {operation.Name}")
                   .Append($" DROP MASKED")
                   .EndCommand();
            }
        }
    }
}
