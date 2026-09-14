using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RetailRescueAI.Backend.Common;

namespace RetailRescueAI.Backend.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string Role { get; set; } = "STAFF"; // STAFF or MANAGER
    public int? StoreId { get; set; }
    public Store? Store { get; set; }
    
    public DateTime CreatedAt { get; set; } = AppClock.Now;
}

public class Store
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string StoreCode { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = AppClock.Now;
}

public class ProductCategory
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g. お弁当 (Bento), サラダ (Salad), etc.
    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;
}

public class Product
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty;
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty; // Japanese: チキン弁当
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public ProductCategory? Category { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; } // Normal selling price in JPY (¥)

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; } // Cost of Goods Sold in JPY (¥)

    [Column(TypeName = "decimal(5,2)")]
    public decimal MinMarginPercent { get; set; } = 15.00m; // Minimum acceptable margin %

    [Column(TypeName = "decimal(5,2)")]
    public decimal MaxDiscountPercent { get; set; } = 50.00m; // Maximum allowed discount %

    [MaxLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    
    public DateTime CreatedAt { get; set; } = AppClock.Now;

    public ICollection<InventoryBatch> Batches { get; set; } = new List<InventoryBatch>();
}

public class InventoryBatch
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string BatchCode { get; set; } = string.Empty; // BATCH-001
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public int InitialQuantity { get; set; }
    public int RemainingQuantity { get; set; }

    public DateTime ProductionDate { get; set; }
    public DateTime ExpiryDate { get; set; } // Specific batch expiry

    [Required, MaxLength(30)]
    public string Status { get; set; } = "AVAILABLE"; // AVAILABLE, AT_RISK, CRITICAL, EXPIRED, SOLD_OUT

    
    public DateTime CreatedAt { get; set; } = AppClock.Now;
    public DateTime UpdatedAt { get; set; } = AppClock.Now;
}

public class Customer
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string CustomerCode { get; set; } = string.Empty; // C000123
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;
    public int Points { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = AppClock.Now;
}

public class CustomerPurchaseHistory
{
    [Key]
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SaleId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public DateTime PurchaseDate { get; set; } = AppClock.Now;
}

public class Promotion
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string PromotionCode { get; set; } = string.Empty; // PROMO-001
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty; // Japanese: 夕方直前割 チキン弁当 20% OFF

    [Required, MaxLength(50)]
    public string PromotionType { get; set; } = "DIRECT_DISCOUNT"; // DIRECT_DISCOUNT, BUY_X_GET_DISCOUNT, BUNDLE

    [Required, MaxLength(30)]
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED, EXPIRED, CANCELLED

    public int? TargetProductId { get; set; }
    public Product? TargetProduct { get; set; }

    public int? TargetBatchId { get; set; }
    public InventoryBatch? TargetBatch { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercent { get; set; }

    public int? ComboProductId { get; set; }
    public Product? ComboProduct { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ComboPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ComboDiscountAmount { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; } // Must satisfy EndTime <= TargetBatch.ExpiryDate (BR-003)

    [Required, MaxLength(30)]
    public string CreatedVia { get; set; } = "AI_AGENT"; // AI_AGENT, CHATBOT, MANAGER

    [MaxLength(100)]
    public string CreatedBy { get; set; } = "OrchestratorAgent";

    [MaxLength(100)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [MaxLength(1000)]
    public string? AiReasoning { get; set; }

    
    public DateTime CreatedAt { get; set; } = AppClock.Now;
    public DateTime UpdatedAt { get; set; } = AppClock.Now;

    public ICollection<PromotionCondition> Conditions { get; set; } = new List<PromotionCondition>();
    public ICollection<PromotionProduct> PromotionProducts { get; set; } = new List<PromotionProduct>();
}

public class PromotionCondition
{
    [Key]
    public int Id { get; set; }
    public int PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    [Required, MaxLength(50)]
    public string ConditionType { get; set; } = "REQUIRED_PRODUCT"; // REQUIRED_PRODUCT, MIN_CART_TOTAL

    public int? RequiredProductId { get; set; }
    public Product? RequiredProduct { get; set; }

    public int RequiredQuantity { get; set; } = 1;
}

public class PromotionProduct
{
    [Key]
    public int Id { get; set; }
    public int PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? BatchId { get; set; }
    public InventoryBatch? Batch { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercent { get; set; }
}

public class Sale
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string TransactionNumber { get; set; } = string.Empty; // TX-20260908-001

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public int? StaffId { get; set; }
    public User? Staff { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = "CASH"; // CASH, CARD, QR_CODE

    [Required, MaxLength(30)]
    public string Status { get; set; } = "COMPLETED";

    
    public DateTime CreatedAt { get; set; } = AppClock.Now;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

public class SaleItem
{
    [Key]
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? BatchId { get; set; }
    public InventoryBatch? Batch { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalPrice { get; set; }

    public int? AppliedPromotionId { get; set; }
    public Promotion? AppliedPromotion { get; set; }
}

public class AIRecommendation
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string RecommendationCode { get; set; } = string.Empty; // REC-001

    public int TargetProductId { get; set; }
    public Product? TargetProduct { get; set; }

    public int TargetBatchId { get; set; }
    public InventoryBatch? TargetBatch { get; set; }

    [Required, MaxLength(50)]
    public string RecommendationType { get; set; } = "DIRECT_DISCOUNT"; // DIRECT_DISCOUNT, BUY_X_GET_DISCOUNT, BUNDLE

    [Required, MaxLength(30)]
    public string RiskLevel { get; set; } = "HIGH"; // LOW, MEDIUM, HIGH, CRITICAL

    [Required, MaxLength(255)]
    public string RecommendedAction { get; set; } = string.Empty; // 20% OFF 17:00 - 22:00

    [Column(TypeName = "decimal(5,2)")]
    public decimal? RecommendedDiscountPercent { get; set; }

    public int? ComboProductId { get; set; }
    public Product? ComboProduct { get; set; }

    [MaxLength(200)]
    public string? ComboProductName { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RecommendedComboPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RecommendedComboSavings { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public int ExpectedSales { get; set; }
    public int ExpectedWasteReduction { get; set; } // units saved

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedRevenue { get; set; }

    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED

    public int? CreatedPromotionId { get; set; }

    
    public DateTime CreatedAt { get; set; } = AppClock.Now;
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    public ICollection<AIRecommendationEvidence> Evidences { get; set; } = new List<AIRecommendationEvidence>();
}

public class AIRecommendationEvidence
{
    [Key]
    public int Id { get; set; }
    public int RecommendationId { get; set; }
    public AIRecommendation? Recommendation { get; set; }

    [Required, MaxLength(50)]
    public string EvidenceKey { get; set; } = string.Empty; // remaining_stock, hours_until_expiry, average_daily_sales, etc.

    [Required, MaxLength(255)]
    public string EvidenceValue { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}

public class PromotionResult
{
    [Key]
    public int Id { get; set; }
    public int PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? BatchId { get; set; }
    public InventoryBatch? Batch { get; set; }

    public int InitialStock { get; set; }
    public int StockBeforePromotion { get; set; }
    public int UnitsSold { get; set; }
    public int UnitsRemaining { get; set; }
    public int ExpiredUnits { get; set; }

    public int ExpectedSales { get; set; }
    public int ActualSales { get; set; }

    public int ExpectedWasteAvoided { get; set; }
    public int ActualWasteAvoided { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal WasteReductionRate { get; set; } // %

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualRevenue { get; set; }

    public DateTime EvaluatedAt { get; set; } = AppClock.Now;
}

