namespace WorkCosts.Models;

public class ItemOfWork
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GarageJobId { get; set; }
    public GarageJob? GarageJob { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int? OdometerMiles { get; set; }
    public Guid? CarId { get; set; }
    public Car? Car { get; set; }
    public Guid? CarDetailsId { get; set; }
    public CarDetails? CarDetails { get; set; }
}
