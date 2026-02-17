namespace ComplexHierarchy.Domain.Models;

public class Manager : Employee
{
    public string Department { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
}
