using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.Extensions
{
    public static class PropertyBuilderExtensions
    {
        /// <summary>
        /// Add dynamic data masking (https://docs.microsoft.com/en-us/sql/relational-databases/security/dynamic-data-masking)
        /// </summary>
        /// <seealso cref="Extensions.DynamicDataMasking.MaskingFunctions"/>
        public static PropertyBuilder<T> HasDataMask<T>(this PropertyBuilder<T> propertyBuilder, string pattern) 
            => propertyBuilder.HasAnnotation(AnnotationConstants.DynamicDataMasking, pattern);
    }
}
