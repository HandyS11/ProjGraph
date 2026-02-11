namespace ComplexHierarchy.Domain.Models;

public class Employee : Person
{
    public string EmployeeId { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
}
