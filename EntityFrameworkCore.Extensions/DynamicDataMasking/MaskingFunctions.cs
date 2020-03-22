namespace EntityFrameworkCore.Extensions.DynamicDataMasking
{
    /// <summary>
    /// Full description of different masking functions is available in <see href="https://docs.microsoft.com/en-us/sql/relational-databases/security/dynamic-data-masking">MS documentation</see>
    /// </summary>
    public static class MaskingFunctions
    {
        public static string Default() => "default()";
        public static string Email() => "email()";
        public static string Random(int startRange, int endRange) => $"random({startRange}, {endRange})";
        public static string Partial(int prefix, string padding, int suffix) => $"partial({prefix}, \"{padding}\", {suffix})";
    }
}
