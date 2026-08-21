using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public class WorkCostsDbContext : DbContext
{
    public WorkCostsDbContext(DbContextOptions<WorkCostsDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductJob> ProductJobs => Set<ProductJob>();
    public DbSet<ProductEquivalent> ProductEquivalents => Set<ProductEquivalent>();
    public DbSet<WorkJob> WorkJobs => Set<WorkJob>();
    public DbSet<WorkJobItem> WorkJobItems => Set<WorkJobItem>();
    public DbSet<CachedWebPage> CachedWebPages => Set<CachedWebPage>();
    public DbSet<CachedWebImage> CachedWebImages => Set<CachedWebImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.GaragePrice).HasPrecision(18, 2);
            e.Property(x => x.NotesMarkdown).HasMaxLength(8000);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Vendor).HasMaxLength(200);
            e.Property(x => x.Source).HasMaxLength(100);
            e.Property(x => x.Manufacturer).HasMaxLength(200);
            e.Property(x => x.Url).HasMaxLength(2000);
            e.Property(x => x.ManufacturerReference).HasMaxLength(200);
            e.Property(x => x.Ean).HasMaxLength(32);
            e.Property(x => x.Variation).HasMaxLength(200);
            e.Property(x => x.OemEquivalent).HasMaxLength(500);
            e.Property(x => x.ExtraYaml).HasMaxLength(8000);
            e.Property(x => x.PricePoint).HasMaxLength(20);
            e.Property(x => x.ImageContentType).HasMaxLength(100);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.ImageBlob).HasColumnType("BLOB");
            e.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductJob>(e =>
        {
            e.HasKey(x => new { x.ProductId, x.JobId });
            e.HasOne(x => x.Product)
                .WithMany(x => x.ProductJobs)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Job)
                .WithMany(x => x.ProductJobs)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductEquivalent>(e =>
        {
            e.HasKey(x => new { x.ProductId, x.EquivalentProductId });
            e.ToTable(t => t.HasCheckConstraint("CK_ProductEquivalent_NotSelf", "ProductId <> EquivalentProductId"));
            e.HasOne(x => x.Product)
                .WithMany(x => x.EquivalentLinks)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EquivalentProduct)
                .WithMany(x => x.EquivalentOfLinks)
                .HasForeignKey(x => x.EquivalentProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Job)
                .WithMany(x => x.WorkJobs)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkJobItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UnitCostSnapshot).HasPrecision(18, 2);
            e.HasOne(x => x.WorkJob)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.WorkJobId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product)
                .WithMany(x => x.WorkJobItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.WorkJobId, x.ProductId }).IsUnique();
        });

        modelBuilder.Entity<CachedWebPage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PageUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Domain).HasMaxLength(200).IsRequired();
            e.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.PageUrl).IsUnique();
            e.HasIndex(x => x.Domain);
        });

        modelBuilder.Entity<CachedWebImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PageUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Domain).HasMaxLength(200).IsRequired();
            e.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.PageUrl, x.ImageUrl }).IsUnique();
            e.HasIndex(x => x.Domain);
        });
    }
}
