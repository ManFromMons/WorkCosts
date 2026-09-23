namespace WorkCosts.Models;

public class CarDetails
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ModelNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? EndYear { get; set; }

    /// <summary>Uppercase Make + Model + ModelNumber + Year. Unique.</summary>
    public string TypeKey { get; set; } = string.Empty;

    public ICollection<JobCarDetails> JobLinks { get; set; } = new List<JobCarDetails>();
}
