using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Provides SQL Server configuration helpers for entity types.
/// </summary>
public static class EntityTypeBuilderExtensions
{
    /// <summary>
    /// Configures a SQL Server spatial index for the selected property.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyExpression">An expression selecting the spatial property.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that ordinary index configuration can be chained.</returns>
    public static IndexBuilder<TEntity> HasSpatialIndex<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, object?>> propertyExpression,
        Action<SpatialIndexOptionsBuilder>? configure = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(propertyExpression);

        var indexBuilder = entityTypeBuilder.HasIndex(propertyExpression);
        indexBuilder.HasAnnotation(AnnotationConstants.SpatialIndex, true);
        configure?.Invoke(new SpatialIndexOptionsBuilder(indexBuilder.Metadata));

        return indexBuilder;
    }

    /// <summary>
    /// Configures a SQL Server spatial index for the named property.
    /// </summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyName">The spatial property name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that ordinary index configuration can be chained.</returns>
    public static IndexBuilder HasSpatialIndex(
        this EntityTypeBuilder entityTypeBuilder,
        string propertyName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var indexBuilder = entityTypeBuilder.HasIndex(propertyName);
        indexBuilder.HasAnnotation(AnnotationConstants.SpatialIndex, true);
        configure?.Invoke(new SpatialIndexOptionsBuilder(indexBuilder.Metadata));

        return indexBuilder;
    }
}
