namespace WorkCosts.Models;

public class ProductJob
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid JobId { get; set; }
    public Job? Job { get; set; }
}
