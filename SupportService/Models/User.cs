namespace SupportService.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsEmployee { get; set; }

    public List<Ticket> Tickets { get; set; } = new();

    public List<Comment> Comments { get; set; } = new();
}