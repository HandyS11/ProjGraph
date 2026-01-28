using SimpleHierarchy.Base;

namespace SimpleHierarchy.Models;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Address PrimaryAddress { get; set; } = new();
    public List<Address> ShippingAddresses { get; set; } = [];
}