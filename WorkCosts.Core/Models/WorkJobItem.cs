namespace WorkCosts.Models;

public class WorkJobItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkJobId { get; set; }
    public WorkJob? WorkJob { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public short Quantity { get; set; } = 1;
    public decimal UnitCostSnapshot { get; set; }
}
