namespace WorkCosts.Models;

public class GarageJobRepeatCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GarageJobId { get; set; }
    public GarageJob? GarageJob { get; set; }
    public GarageJobRepeatKind Kind { get; set; }
    public int Amount { get; set; }
    public int Unit { get; set; }
    public int SortOrder { get; set; }
}
