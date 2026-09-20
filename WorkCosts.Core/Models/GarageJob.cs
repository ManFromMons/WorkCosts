namespace WorkCosts.Models;

public class GarageJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public GarageJobTargetKind TargetKind { get; set; } = GarageJobTargetKind.Car;
    public string TargetLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string IconRelativePath { get; set; } = string.Empty;
    public string IconContentType { get; set; } = string.Empty;
    public GarageJobRepeatCombine RepeatCombine { get; set; } = GarageJobRepeatCombine.WhicheverFirst;
    public DateOnly? IntervalAnchorDate { get; set; }

    public ICollection<GarageJobRepeatCondition> RepeatConditions { get; set; } = new List<GarageJobRepeatCondition>();
    public ICollection<GarageJobRequiredProduct> RequiredProducts { get; set; } = new List<GarageJobRequiredProduct>();
    public ICollection<GarageJobReferencedJob> ReferencedJobs { get; set; } = new List<GarageJobReferencedJob>();
    public ICollection<ItemOfWork> ItemsOfWork { get; set; } = new List<ItemOfWork>();
}
