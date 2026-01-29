namespace SimpleHierarchy.Dtos;

public record UserDto
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}