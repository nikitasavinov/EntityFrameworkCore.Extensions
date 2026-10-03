using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace EntityFrameworkCore.Extensions.Services;

#pragma warning disable EF1001 // The query provider type is the only way to see whether this library replaced the compiler.

/// <summary>
/// Rejects hint calls on an EF Core query whose context was not configured with this library.
/// Without the SQL generator replacement, EF Core prints the hint tag as a comment and the statement runs unhinted.
/// </summary>
internal static class HintConfiguration
{
    private static readonly FieldInfo? QueryCompilerField = typeof(EntityQueryProvider)
        .GetField("_queryCompiler", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void EnsureConfigured(IQueryable source)
    {
        if (source.Provider is not EntityQueryProvider provider)
        {
            return;
        }

        if (QueryCompilerField?.GetValue(provider) is ExtendedQueryCompiler)
        {
            return;
        }

        throw new InvalidOperationException(
            "Call UseEntityFrameworkCoreExtensions() when configuring the DbContext before using query or table hints. " +
            "Otherwise a hint such as UPDLOCK is ignored and does not take locks.");
    }
}

#pragma warning restore EF1001
