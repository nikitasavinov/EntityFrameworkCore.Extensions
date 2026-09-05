namespace EntityFrameworkCore.Extensions;

/// <summary>
/// Creates SQL Server dynamic data masking function expressions.
/// </summary>
/// <seealso href="https://learn.microsoft.com/sql/relational-databases/security/dynamic-data-masking" />
public static class MaskingFunctions
{
    /// <summary>Creates the default masking function.</summary>
    /// <returns><c>default()</c>.</returns>
    public static string Default() => "default()";

    /// <summary>Creates the email masking function.</summary>
    /// <returns><c>email()</c>.</returns>
    public static string Email() => "email()";

    /// <summary>Creates a random-number masking function.</summary>
    /// <param name="startRange">The inclusive lower bound.</param>
    /// <param name="endRange">The inclusive upper bound.</param>
    /// <returns>The SQL Server masking function expression.</returns>
    public static string Random(int startRange, int endRange) => $"random({startRange}, {endRange})";

    /// <summary>Creates a partial-string masking function.</summary>
    /// <param name="prefix">The number of leading characters to expose.</param>
    /// <param name="padding">The masking text.</param>
    /// <param name="suffix">The number of trailing characters to expose.</param>
    /// <returns>The SQL Server masking function expression.</returns>
    /// <exception cref="ArgumentException"><paramref name="padding" /> contains a double quote.</exception>
    public static string Partial(int prefix, string padding, int suffix)
    {
        ArgumentNullException.ThrowIfNull(padding);
        if (padding.Contains('"'))
        {
            throw new ArgumentException("Mask padding cannot contain a double quote.", nameof(padding));
        }

        return $"partial({prefix}, \"{padding}\", {suffix})";
    }
}
