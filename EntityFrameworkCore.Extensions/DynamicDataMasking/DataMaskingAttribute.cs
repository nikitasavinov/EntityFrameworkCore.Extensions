using System;

namespace EntityFrameworkCore.Extensions.DynamicDataMasking
{
    public class DataMaskingAttribute : Attribute
    {
        public string MaskingFunction { get; set; }

        public DataMaskingAttribute()
        {
            MaskingFunction = MaskingFunctions.Default();
        }
    }
}
