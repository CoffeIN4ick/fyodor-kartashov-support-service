namespace SupportService.Models;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ReactionTimeMinutes { get; set; }

    public List<Ticket> Tickets { get; set; } = new();
}