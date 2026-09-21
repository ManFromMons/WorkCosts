namespace WorkCosts.Models;

public class Car
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ModelNumber { get; set; } = string.Empty;
    public string EngineType { get; set; } = string.Empty;
    public string Vrm { get; set; } = string.Empty;

    /// <summary>Uppercase VRM with spaces removed. Unique among rows where <see cref="DeletedAt"/> is null.</summary>
    public string VrmKey { get; set; } = string.Empty;

    public int Year { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string ImageRelativePath { get; set; } = string.Empty;
    public string ImageContentType { get; set; } = string.Empty;
    public string VehicleOrderJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
