namespace SimpleHierarchy.Models;

public class Admin : User
{
    public string Permissions { get; set; } = "All";
    public DateTime? LastLogin { get; set; }
}
