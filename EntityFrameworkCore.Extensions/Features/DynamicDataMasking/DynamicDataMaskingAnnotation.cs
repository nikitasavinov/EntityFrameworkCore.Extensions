using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.Extensions.Services;

/// <summary>
/// Reads and validates dynamic data masking annotations.
/// </summary>
internal static class DynamicDataMaskingAnnotation
{
    public static string? GetMaskingFunction(
        IReadOnlyAnnotatable annotatable,
        string? schema,
        string table,
        string column)
    {
        var annotation = annotatable.FindAnnotation(AnnotationConstants.DynamicDataMasking);
        return annotation is null
            ? null
            : GetMaskingFunction(annotation, schema, table, column);
    }

    public static string GetMaskingFunction(
        IAnnotation annotation,
        string? schema,
        string table,
        string column)
    {
        if (annotation.Value is not string maskingFunction || string.IsNullOrWhiteSpace(maskingFunction))
        {
            throw new InvalidOperationException(
                $"The '{AnnotationConstants.DynamicDataMasking}' annotation on " +
                $"'{FormatColumnName(schema, table, column)}' must contain a non-empty string.");
        }

        return maskingFunction;
    }

    public static string FormatColumnName(string? schema, string table, string column)
        => string.IsNullOrEmpty(schema)
            ? $"{table}.{column}"
            : $"{schema}.{table}.{column}";
}
