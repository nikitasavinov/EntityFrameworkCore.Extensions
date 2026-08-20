using Microsoft.EntityFrameworkCore.Migrations;

namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Provides additional operations for <see cref="MigrationBuilder" />.
/// </summary>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Adds the SQL contained in a file to a migration.
    /// </summary>
    /// <remarks>
    /// The file must be available when the migration is constructed. Set its project
    /// <c>CopyToOutputDirectory</c> metadata to <c>PreserveNewest</c> or <c>Always</c>, and pass a path
    /// rooted at <see cref="AppContext.BaseDirectory" />.
    /// </remarks>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="path">The path to the SQL file.</param>
    /// <returns>The same builder so that migration calls can be chained.</returns>
    public static MigrationBuilder SqlFile(this MigrationBuilder migrationBuilder, string path)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var sql = File.ReadAllText(path);
        migrationBuilder.Sql(sql);

        return migrationBuilder;
    }
}
