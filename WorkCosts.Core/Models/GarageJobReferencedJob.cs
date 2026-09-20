namespace WorkCosts.Models;

public class GarageJobReferencedJob
{
    public Guid GarageJobId { get; set; }
    public GarageJob? GarageJob { get; set; }

    public Guid JobId { get; set; }
    public Job? Job { get; set; }

    public int SortOrder { get; set; }
}
