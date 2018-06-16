using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.Extensions
{
    public static class DbContextOptionsBuilderExtensions
    {
        /// <summary>
        /// Configure EFCore to throw when SQL can't be generated (instead of loading everything to the client)
        /// </summary>
        /// <param name="optionsBuilder"></param>
        /// <returns></returns>
        public static DbContextOptionsBuilder ThrowOnQueryClientEvaluation(this DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(c => c.Throw(RelationalEventId.QueryClientEvaluationWarning));
            return optionsBuilder;
        }
    }
}
