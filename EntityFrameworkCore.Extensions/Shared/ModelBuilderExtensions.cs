using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Provides model-wide configuration helpers for <see cref="ModelBuilder" />.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Replaces the delete behavior of every foreign key currently present in the model.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="deleteBehaviour">The delete behavior to apply.</param>
    public static void OverrideDeleteBehaviour(
        this ModelBuilder modelBuilder,
        DeleteBehavior deleteBehaviour = DeleteBehavior.Restrict)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var relationships = modelBuilder.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .ToList();

        foreach (var relationship in relationships)
        {
            relationship.DeleteBehavior = deleteBehaviour;
        }
    }
}
