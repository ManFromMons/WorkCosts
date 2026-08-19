namespace WorkCosts.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public string Vendor { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ManufacturerReference { get; set; } = string.Empty;
    public string Ean { get; set; } = string.Empty;
    public string Variation { get; set; } = string.Empty;
    public string OemEquivalent { get; set; } = string.Empty;
    public string PricePoint { get; set; } = string.Empty;
    public byte[]? ImageBlob { get; set; }
    public string? ImageContentType { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>When true, product is available for every Work Job (e.g. general tools).</summary>
    public bool IsAllJobs { get; set; }

    public ICollection<ProductJob> ProductJobs { get; set; } = new List<ProductJob>();
    public ICollection<WorkJobItem> WorkJobItems { get; set; } = new List<WorkJobItem>();
    public ICollection<ProductEquivalent> EquivalentLinks { get; set; } = new List<ProductEquivalent>();
    public ICollection<ProductEquivalent> EquivalentOfLinks { get; set; } = new List<ProductEquivalent>();
}
