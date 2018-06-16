using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static void OverrideDeleteBehaviour(this ModelBuilder modelBuilder, DeleteBehavior deleteBehaviour = DeleteBehavior.Restrict)
        {
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = deleteBehaviour;
            }
        }
    }
}
