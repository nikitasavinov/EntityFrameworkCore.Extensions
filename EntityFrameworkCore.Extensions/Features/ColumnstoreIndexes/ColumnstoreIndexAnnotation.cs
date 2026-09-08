using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Extensions.Services;

internal static class ColumnstoreIndexAnnotation
{
    private static readonly HashSet<string> SupportedColumnTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bigint", "int", "smallint", "tinyint", "bit",
        "decimal", "numeric", "money", "smallmoney", "float", "real",
        "date", "time", "datetime", "datetime2", "datetimeoffset", "smalldatetime",
        "char", "nchar", "varchar", "nvarchar", "binary", "varbinary", "uniqueidentifier"
    };

    public static bool IsColumnstoreIndex(IReadOnlyAnnotatable annotatable, string indexName)
    {
        var annotation = annotatable.FindAnnotation(AnnotationConstants.ColumnstoreIndex);
        if (annotation is null)
        {
            return false;
        }

        return annotation.Value is bool value
            ? value
            : throw new InvalidOperationException(
                $"The '{annotation.Name}' annotation on index '{indexName}' must contain a Boolean.");
    }

    public static void ValidateIndex(
        IReadOnlyAnnotatable annotatable,
        string indexName,
        int columnCount,
        bool isUnique,
        IReadOnlyList<bool>? isDescending)
    {
        if (columnCount is < 1 or > 1024)
        {
            throw new InvalidOperationException($"Columnstore index '{indexName}' must target between 1 and 1024 columns.");
        }

        if (isUnique)
        {
            throw new InvalidOperationException($"Columnstore index '{indexName}' cannot be unique.");
        }

        if (isDescending is not null)
        {
            throw new InvalidOperationException($"Columnstore index '{indexName}' cannot specify sort order.");
        }

        if (annotatable[AnnotationConstants.SpatialIndex] is true)
        {
            throw new InvalidOperationException($"Index '{indexName}' cannot be both columnstore and spatial.");
        }

        if (annotatable["SqlServer:MemoryOptimized"] is true)
        {
            throw new InvalidOperationException(
                $"Nonclustered columnstore index '{indexName}' cannot target a memory-optimized table.");
        }

        foreach (var annotation in annotatable.GetAnnotations())
        {
            if (!annotation.Name.StartsWith("SqlServer:", StringComparison.Ordinal))
            {
                continue;
            }

            // EF omits disabled ONLINE and SORT_IN_TEMPDB options from the generated SQL.
            if ((annotation.Name is "SqlServer:Clustered" or "SqlServer:Online" or "SqlServer:SortInTempDb" or "SqlServer:MemoryOptimized"
                    && annotation.Value is false)
                || (annotation.Name == "SqlServer:Include" && annotation.Value is Array { Length: 0 }))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Columnstore index '{indexName}' does not support the '{annotation.Name}' option. " +
                "Only nonclustered indexes with an optional filter are supported.");
        }
    }

    public static bool IsColumnstoreIndex(ITableIndex index)
    {
        var indexName = $"{index.Table.SchemaQualifiedName}.{index.Name}";
        var mappedIndexes = index.MappedIndexes.ToList();
        var columnstoreCount = mappedIndexes.Count(mappedIndex => IsColumnstoreIndex(mappedIndex, indexName));
        if (columnstoreCount > 0 && columnstoreCount != mappedIndexes.Count)
        {
            throw new InvalidOperationException(
                $"Relational index '{indexName}' combines columnstore and ordinary model indexes.");
        }

        return columnstoreCount > 0;
    }

    public static void ValidateTable(ITable table, string databaseIndexName, string indexName)
    {
        // Inspect every shared index before reporting the table-wide limit, so a mixed mapping
        // is diagnosed even when a different columnstore index is visited first.
        var otherIndexCount = table.Indexes.Count(index => IsColumnstoreIndex(index) && index.Name != databaseIndexName);
        if (otherIndexCount > 0)
        {
            throw new InvalidOperationException(
                $"Table '{table.SchemaQualifiedName}' can have only one columnstore index.");
        }

        if (table.EntityTypeMappings.Any(mapping => mapping.TypeBase is IEntityType entityType && entityType.IsMemoryOptimized()))
        {
            throw new InvalidOperationException(
                $"Nonclustered columnstore index '{indexName}' cannot target a memory-optimized table.");
        }
    }

    public static void ValidateColumn(IColumn column, string indexName)
    {
        var storeType = column.StoreType.Trim();
        var parameterStart = storeType.IndexOf('(', StringComparison.Ordinal);
        var typeName = (parameterStart < 0 ? storeType : storeType[..parameterStart]).Trim();
        var parameters = parameterStart < 0 ? string.Empty : storeType[(parameterStart + 1)..].TrimEnd(')').Trim();
        if (!SupportedColumnTypes.Contains(typeName) || parameters.Equals("max", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Columnstore index '{indexName}' targets column '{column.Name}' with unsupported store type '{column.StoreType}'. " +
                "Nonclustered columnstore indexes require supported built-in SQL Server types without MAX lengths.");
        }

        if (column.ComputedColumnSql is not null)
        {
            throw new InvalidOperationException($"Columnstore index '{indexName}' cannot include computed column '{column.Name}'.");
        }

        var storeObject = StoreObjectIdentifier.Table(column.Table.Name, column.Table.Schema);
        if (column.PropertyMappings.Any(mapping => mapping.Property.IsSparse(storeObject) == true))
        {
            throw new InvalidOperationException($"Columnstore index '{indexName}' cannot include sparse column '{column.Name}'.");
        }
    }
}
