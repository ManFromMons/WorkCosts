namespace WorkCosts.Models;

public class WorkJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Job? Job { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public ICollection<WorkJobItem> Items { get; set; } = new List<WorkJobItem>();
}
