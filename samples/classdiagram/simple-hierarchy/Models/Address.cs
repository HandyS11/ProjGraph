namespace SimpleHierarchy.Models;

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = "USA";
    public Localisation Location { get; set; } = new(0, 0, 0);
}
