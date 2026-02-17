namespace ComplexHierarchy.Domain.Models;

public class CEO : Manager
{
    public string GlobalStrategy { get; set; } = string.Empty;
    public decimal StockOptions { get; set; }
    public ICollection<Manager> RegionalManagers { get; set; } = new List<Manager>();
}
