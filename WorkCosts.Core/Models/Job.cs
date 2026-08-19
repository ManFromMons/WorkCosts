namespace WorkCosts.Models;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal GaragePrice { get; set; }
    public string NotesMarkdown { get; set; } = string.Empty;

    /// <summary>Estimated duration in whole minutes.</summary>
    public int DurationMinutes { get; set; }

    public ICollection<ProductJob> ProductJobs { get; set; } = new List<ProductJob>();
    public ICollection<WorkJob> WorkJobs { get; set; } = new List<WorkJob>();
}
