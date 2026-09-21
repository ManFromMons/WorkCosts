namespace WorkCosts.Models;

public class JobCarDetails
{
    public Guid JobId { get; set; }
    public Job? Job { get; set; }

    public Guid CarDetailsId { get; set; }
    public CarDetails? CarDetails { get; set; }

    public int SortOrder { get; set; }
}
