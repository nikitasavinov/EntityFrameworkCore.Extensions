using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.Extensions;

public static partial class EntityTypeBuilderExtensions
{
    /// <summary>Configures a SQL Server nonclustered columnstore index on the selected properties.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertiesExpression">An expression selecting one property or an anonymous object of properties.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    /// <remarks>
    /// Only one columnstore index is allowed per table. Unique, clustered, included-column, sort-order,
    /// and additional SQL Server index options are not supported. Use SQL Server 2016 or later.
    /// </remarks>
    public static IndexBuilder<TEntity> HasColumnstoreIndex<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, object?>> propertiesExpression)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(propertiesExpression);

        return entityTypeBuilder.HasIndex(propertiesExpression).HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a named SQL Server nonclustered columnstore index on the selected properties.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertiesExpression">An expression selecting one property or an anonymous object of properties.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    /// <remarks>Use a distinct model index name to retain an ordinary index on the same properties.</remarks>
    public static IndexBuilder<TEntity> HasColumnstoreIndex<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, object?>> propertiesExpression,
        string modelIndexName)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(propertiesExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return entityTypeBuilder.HasIndex(propertiesExpression, modelIndexName)
            .HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a SQL Server nonclustered columnstore index on the named properties.</summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyNames">The property names.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    public static IndexBuilder HasColumnstoreIndex(this EntityTypeBuilder entityTypeBuilder, params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);

        return entityTypeBuilder.HasIndex(propertyNames).HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a named SQL Server nonclustered columnstore index on the named properties.</summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="propertyNames">The property names.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    public static IndexBuilder HasColumnstoreIndex(
        this EntityTypeBuilder entityTypeBuilder,
        string[] propertyNames,
        string modelIndexName)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return entityTypeBuilder.HasIndex(propertyNames, modelIndexName).HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a SQL Server nonclustered columnstore index on properties of an owned entity.</summary>
    /// <typeparam name="TOwnerEntity">The owner entity type.</typeparam>
    /// <typeparam name="TDependentEntity">The owned entity type.</typeparam>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertiesExpression">An expression selecting one property or an anonymous object of properties.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    public static IndexBuilder<TDependentEntity> HasColumnstoreIndex<TOwnerEntity, TDependentEntity>(
        this OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ownedNavigationBuilder,
        Expression<Func<TDependentEntity, object?>> propertiesExpression)
        where TOwnerEntity : class
        where TDependentEntity : class
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentNullException.ThrowIfNull(propertiesExpression);

        return ownedNavigationBuilder.HasIndex(propertiesExpression).HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a named SQL Server nonclustered columnstore index on properties of an owned entity.</summary>
    /// <typeparam name="TOwnerEntity">The owner entity type.</typeparam>
    /// <typeparam name="TDependentEntity">The owned entity type.</typeparam>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertiesExpression">An expression selecting one property or an anonymous object of properties.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    public static IndexBuilder<TDependentEntity> HasColumnstoreIndex<TOwnerEntity, TDependentEntity>(
        this OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ownedNavigationBuilder,
        Expression<Func<TDependentEntity, object?>> propertiesExpression,
        string modelIndexName)
        where TOwnerEntity : class
        where TDependentEntity : class
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentNullException.ThrowIfNull(propertiesExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return ownedNavigationBuilder.HasIndex(propertiesExpression, modelIndexName)
            .HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a SQL Server nonclustered columnstore index on named properties of an owned entity.</summary>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertyNames">The property names.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    public static IndexBuilder HasColumnstoreIndex(this OwnedNavigationBuilder ownedNavigationBuilder, params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);

        return ownedNavigationBuilder.HasIndex(propertyNames).HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }

    /// <summary>Configures a named SQL Server nonclustered columnstore index on named properties of an owned entity.</summary>
    /// <param name="ownedNavigationBuilder">The owned-navigation builder.</param>
    /// <param name="propertyNames">The property names.</param>
    /// <param name="modelIndexName">The EF Core model index name.</param>
    /// <returns>The index builder, supporting <c>HasDatabaseName()</c> and <c>HasFilter()</c>.</returns>
    public static IndexBuilder HasColumnstoreIndex(
        this OwnedNavigationBuilder ownedNavigationBuilder,
        string[] propertyNames,
        string modelIndexName)
    {
        ArgumentNullException.ThrowIfNull(ownedNavigationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelIndexName);

        return ownedNavigationBuilder.HasIndex(propertyNames, modelIndexName).HasAnnotation(AnnotationConstants.ColumnstoreIndex, true);
    }
}
