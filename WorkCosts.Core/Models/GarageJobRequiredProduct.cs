namespace WorkCosts.Models;

public class GarageJobRequiredProduct
{
    public Guid GarageJobId { get; set; }
    public GarageJob? GarageJob { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public short Quantity { get; set; } = 1;
    public int SortOrder { get; set; }
}
