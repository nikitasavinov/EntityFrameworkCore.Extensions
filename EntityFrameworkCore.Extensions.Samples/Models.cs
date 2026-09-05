using NetTopologySuite.Geometries;

namespace EntityFrameworkCore.Extensions.Samples;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Surname2 { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int DiscountCardNumber { get; set; }
    public string SampleProperty1 { get; set; } = string.Empty;
    public string SampleProperty2 { get; set; } = string.Empty;

    public IList<Order> Orders { get; set; } = new List<Order>();
}

public class Order
{
    public int Id { get; set; }
    public DateTime Created { get; set; }
}

public class Place
{
    public int Id { get; set; }
    public Point Location { get; set; } = new(0, 0) { SRID = 4326 };
}

public class Region
{
    public int Id { get; set; }
    public Polygon Boundary { get; set; } = null!;
}
