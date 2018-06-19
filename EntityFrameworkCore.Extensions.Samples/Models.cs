using System;
using System.Collections.Generic;
using EntityFrameworkCore.Extensions.DynamicDataMasking;

namespace EntityFrameworkCore.Extensions.Samples
{
    public class Customer
    {
        public int Id { get; set; }

        [DataMasking(MaskingFunction = "default()")]
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Phone { get; set; }
        public int DiscountCardNumber { get; set; }
        public string SampleProperty1 { get; set; }
        public string SampleProperty2 { get; set; }

        public IList<Order> Orders { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }

        public DateTime Created { get; set; }
    }
}
