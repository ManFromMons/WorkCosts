namespace WorkCosts.Models;

public class ProductEquivalent
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid EquivalentProductId { get; set; }
    public Product? EquivalentProduct { get; set; }
}
