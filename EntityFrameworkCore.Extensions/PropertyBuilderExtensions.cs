using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Provides SQL Server configuration helpers for entity properties.
/// </summary>
public static class PropertyBuilderExtensions
{
    /// <summary>
    /// Adds a SQL Server dynamic data mask to a property.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <param name="pattern">The SQL Server masking function.</param>
    /// <returns>The same builder so that property configuration can be chained.</returns>
    /// <seealso cref="MaskingFunctions" />
    public static PropertyBuilder<T> HasDataMask<T>(this PropertyBuilder<T> propertyBuilder, string pattern)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return propertyBuilder.HasAnnotation(AnnotationConstants.DynamicDataMasking, pattern);
    }
}
