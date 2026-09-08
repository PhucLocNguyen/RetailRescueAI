using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPurchaseHistory> CustomerPurchaseHistories => Set<CustomerPurchaseHistory>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionCondition> PromotionConditions => Set<PromotionCondition>();
    public DbSet<PromotionProduct> PromotionProducts => Set<PromotionProduct>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<AIRecommendation> AIRecommendations => Set<AIRecommendation>();
    public DbSet<AIRecommendationEvidence> AIRecommendationEvidences => Set<AIRecommendationEvidence>();
    public DbSet<PromotionResult> PromotionResults => Set<PromotionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.ProductCode)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Barcode);

        modelBuilder.Entity<InventoryBatch>()
            .HasIndex(b => b.BatchCode)
            .IsUnique();

        modelBuilder.Entity<InventoryBatch>()
            .HasIndex(b => b.ExpiryDate);

        modelBuilder.Entity<Promotion>()
            .HasIndex(p => p.PromotionCode)
            .IsUnique();

        modelBuilder.Entity<Sale>()
            .HasIndex(s => s.TransactionNumber)
            .IsUnique();

        // Relationships & Foreign keys
        modelBuilder.Entity<InventoryBatch>()
            .HasOne(b => b.Product)
            .WithMany(p => p.Batches)
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<SaleItem>()
            .HasOne(si => si.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(si => si.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SaleItem>()
            .HasOne(si => si.Product)
            .WithMany()
            .HasForeignKey(si => si.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<SaleItem>()
            .HasOne(si => si.Batch)
            .WithMany()
            .HasForeignKey(si => si.BatchId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PromotionProduct>()
            .HasOne(pp => pp.Promotion)
            .WithMany(p => p.PromotionProducts)
            .HasForeignKey(pp => pp.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PromotionCondition>()
            .HasOne(pc => pc.Promotion)
            .WithMany(p => p.Conditions)
            .HasForeignKey(pc => pc.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AIRecommendation>()
            .HasOne(r => r.TargetProduct)
            .WithMany()
            .HasForeignKey(r => r.TargetProductId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<AIRecommendation>()
            .HasOne(r => r.TargetBatch)
            .WithMany()
            .HasForeignKey(r => r.TargetBatchId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<AIRecommendationEvidence>()
            .HasOne(e => e.Recommendation)
            .WithMany(r => r.Evidences)
            .HasForeignKey(e => e.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomerPurchaseHistory>()
            .HasOne(h => h.Customer)
            .WithMany()
            .HasForeignKey(h => h.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<CustomerPurchaseHistory>()
            .HasOne(h => h.Product)
            .WithMany()
            .HasForeignKey(h => h.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PromotionResult>()
            .HasOne(pr => pr.Promotion)
            .WithMany()
            .HasForeignKey(pr => pr.PromotionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PromotionResult>()
            .HasOne(pr => pr.Product)
            .WithMany()
            .HasForeignKey(pr => pr.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PromotionResult>()
            .HasOne(pr => pr.Batch)
            .WithMany()
            .HasForeignKey(pr => pr.BatchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
