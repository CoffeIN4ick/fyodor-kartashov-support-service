namespace SupportService.Models;

public class Ticket
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "new";

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public List<Comment> Comments { get; set; } = new();
}