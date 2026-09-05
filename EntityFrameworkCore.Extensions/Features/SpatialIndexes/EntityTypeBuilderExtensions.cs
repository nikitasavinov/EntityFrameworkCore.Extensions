using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Provides SQL Server configuration helpers for entity and owned entity types.
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
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The entity must have a primary key
    /// backed by a clustered SQL Server index. Unique, filtered, clustered, included-column, descending, and
    /// other SQL Server index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder<TEntity> HasSpatialIndex<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, object?>> propertyExpression,
        Action<SpatialIndexOptionsBuilder>? configure = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(propertyExpression);

        return ConfigureSpatialIndex(entityTypeBuilder.HasIndex(propertyExpression), configure);
    }

    /// <summary>
    /// Configures a named SQL Server spatial index for the selected property.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyExpression">An expression selecting the spatial property.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The table must have a primary key
    /// backed by a clustered SQL Server index. Other index options are not supported for spatial indexes.
    /// Use a distinct model index name for each spatial index on the same property. Use
    /// <c>HasDatabaseName()</c> to configure the corresponding SQL index name.
    /// </remarks>
    public static IndexBuilder<TEntity> HasSpatialIndex<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, object?>> propertyExpression,
        string modelIndexName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return ConfigureSpatialIndex(entityTypeBuilder.HasIndex(propertyExpression, modelIndexName), configure);
    }

    /// <summary>
    /// Configures a SQL Server spatial index for the named property.
    /// </summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyName">The spatial property name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The entity must have a primary key
    /// backed by a clustered SQL Server index. Unique, filtered, clustered, included-column, descending, and
    /// other SQL Server index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder HasSpatialIndex(
        this EntityTypeBuilder entityTypeBuilder,
        string propertyName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return ConfigureSpatialIndex(entityTypeBuilder.HasIndex(propertyName), configure);
    }

    /// <summary>
    /// Configures a named SQL Server spatial index for the named property.
    /// </summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyName">The spatial property name.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The table must have a primary key
    /// backed by a clustered SQL Server index. Other index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder HasSpatialIndex(
        this EntityTypeBuilder entityTypeBuilder,
        string propertyName,
        string modelIndexName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return ConfigureSpatialIndex(entityTypeBuilder.HasIndex([propertyName], modelIndexName), configure);
    }

    /// <summary>
    /// Configures a SQL Server spatial index for a property of an owned entity.
    /// </summary>
    /// <typeparam name="TOwnerEntity">The owner entity type.</typeparam>
    /// <typeparam name="TDependentEntity">The owned entity type.</typeparam>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertyExpression">An expression selecting the spatial property.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The mapped table must have a primary key
    /// backed by a clustered SQL Server index. Other index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder<TDependentEntity> HasSpatialIndex<TOwnerEntity, TDependentEntity>(
        this OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ownedNavigationBuilder,
        Expression<Func<TDependentEntity, object?>> propertyExpression,
        Action<SpatialIndexOptionsBuilder>? configure = null)
        where TOwnerEntity : class
        where TDependentEntity : class
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentNullException.ThrowIfNull(propertyExpression);

        return ConfigureSpatialIndex(ownedNavigationBuilder.HasIndex(propertyExpression), configure);
    }

    /// <summary>
    /// Configures a named SQL Server spatial index for a property of an owned entity.
    /// </summary>
    /// <typeparam name="TOwnerEntity">The owner entity type.</typeparam>
    /// <typeparam name="TDependentEntity">The owned entity type.</typeparam>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertyExpression">An expression selecting the spatial property.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The mapped table must have a primary key
    /// backed by a clustered SQL Server index. Other index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder<TDependentEntity> HasSpatialIndex<TOwnerEntity, TDependentEntity>(
        this OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ownedNavigationBuilder,
        Expression<Func<TDependentEntity, object?>> propertyExpression,
        string modelIndexName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
        where TOwnerEntity : class
        where TDependentEntity : class
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return ConfigureSpatialIndex(
            ownedNavigationBuilder.HasIndex(propertyExpression, modelIndexName),
            configure);
    }

    /// <summary>
    /// Configures a SQL Server spatial index for a named property of an owned entity.
    /// </summary>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertyName">The spatial property name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The mapped table must have a primary key
    /// backed by a clustered SQL Server index. Other index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder HasSpatialIndex(
        this OwnedNavigationBuilder ownedNavigationBuilder,
        string propertyName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return ConfigureSpatialIndex(ownedNavigationBuilder.HasIndex(propertyName), configure);
    }

    /// <summary>
    /// Configures a named SQL Server spatial index for a named property of an owned entity.
    /// </summary>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertyName">The spatial property name.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <param name="configure">An optional action that configures spatial index options.</param>
    /// <returns>The index builder so that <c>HasDatabaseName()</c> can be chained.</returns>
    /// <remarks>
    /// Map the property explicitly to <c>geography</c> or <c>geometry</c>. The mapped table must have a primary key
    /// backed by a clustered SQL Server index. Other index options are not supported for spatial indexes.
    /// </remarks>
    public static IndexBuilder HasSpatialIndex(
        this OwnedNavigationBuilder ownedNavigationBuilder,
        string propertyName,
        string modelIndexName,
        Action<SpatialIndexOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return ConfigureSpatialIndex(ownedNavigationBuilder.HasIndex([propertyName], modelIndexName), configure);
    }

    private static TIndexBuilder ConfigureSpatialIndex<TIndexBuilder>(
        TIndexBuilder indexBuilder,
        Action<SpatialIndexOptionsBuilder>? configure)
        where TIndexBuilder : IndexBuilder
    {
        indexBuilder.HasAnnotation(AnnotationConstants.SpatialIndex, true);
        configure?.Invoke(new SpatialIndexOptionsBuilder(indexBuilder.Metadata));

        return indexBuilder;
    }
}
