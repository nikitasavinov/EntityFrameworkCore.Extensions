using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // Extending SQL Server's annotation provider requires its provider-internal implementation.

internal sealed partial class ExtendedSqlServerAnnotationProvider
{
    /// <inheritdoc />
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (!designTime)
        {
            yield break;
        }

        var maskingAnnotations = column.PropertyMappings
            .Select(mapping => mapping.Property.FindAnnotation(AnnotationConstants.DynamicDataMasking))
            .OfType<IAnnotation>()
            .ToList();
        if (maskingAnnotations.Count == 0)
        {
            yield break;
        }

        var maskingFunctions = maskingAnnotations
            .Select(annotation => DynamicDataMaskingAnnotation.GetMaskingFunction(
                annotation,
                column.Table.Schema,
                column.Table.Name,
                column.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (maskingFunctions.Count > 1)
        {
            var columnName = DynamicDataMaskingAnnotation.FormatColumnName(
                column.Table.Schema,
                column.Table.Name,
                column.Name);
            throw new InvalidOperationException(
                $"Column '{columnName}' has conflicting dynamic data masks: " +
                $"{string.Join(", ", maskingFunctions.Select(mask => $"'{mask}'"))}.");
        }

        yield return maskingAnnotations[0];
    }
}

#pragma warning restore EF1001
