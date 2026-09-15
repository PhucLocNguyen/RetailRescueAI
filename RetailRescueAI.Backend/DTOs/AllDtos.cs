namespace RetailRescueAI.Backend.DTOs;

// Auth
public record LoginRequest(string Username, string Password);
public record LoginResponse(bool Success, string Message, string Token, UserDto? User);
public record UserDto(int Id, string Username, string FullName, string Role, int? StoreId, string? StoreName);

// POS
public record PosBatchSummaryDto(
    int Id,
    string BatchCode,
    int ProductId,
    int RemainingQuantity,
    DateTime ProductionDate,
    DateTime ExpiryDate,
    double HoursUntilExpiry,
    string ExpiryFormatted,
    bool IsExpired,
    bool IsDiscounted,
    decimal? DiscountPercent,
    decimal? FinalPrice,
    int? PromotionId,
    string? PromotionName
);

public record PosProductDto(
    int Id,
    string ProductCode,
    string Name,
    string Description,
    string CategoryName,
    decimal Price,
    string Barcode,
    string ImageUrl,
    int TotalAvailableStock,
    string EarliestExpiryFormatted,
    List<PosBatchSummaryDto> Batches
);

public record PosRecommendationRequest(
    List<int> ProductIdsInCart,
    List<int>? BatchIdsInCart,
    decimal CurrentSubtotal,
    int? CustomerId
);

public record PosRecommendationItemDto(
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    string PromotionType,
    int TargetProductId,
    string TargetProductName,
    int? TargetBatchId,
    string? TargetBatchCode,
    DateTime? ExpiryDate,
    decimal OriginalPrice,
    decimal DiscountPercent,
    decimal FinalPrice,
    string Message,
    string ActionPrompt, // e.g. "チキン弁当をお買い上げでサラダが20%OFF！"
    int? ComboProductId = null,
    string? ComboProductName = null,
    decimal? ComboPrice = null,
    decimal? SavingsAmount = null,
    string? StaffScript = null // e.g. "「お客様、ご一緒に『宇治緑茶 500ml』はいかがでしょうか？セットで ¥350（¥70お得）でお買い求めいただけます！」"
);

public record PosRecommendationResponse(
    List<PosRecommendationItemDto> Recommendations
);

public record CheckoutItemRequest(
    int ProductId,
    int BatchId,
    int Quantity,
    int? AppliedPromotionId,
    string? ScannedBarcode = null,
    string? BatchCode = null
);

public record CheckoutRequest(
    int? CustomerId,
    string PaymentMethod,
    List<CheckoutItemRequest> Items,
    decimal ReceivedAmount
);

public record CheckoutResponse(
    bool Success,
    string Message,
    string TransactionNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal ChangeAmount,
    DateTime CreatedAt,
    List<ReceiptItemDto> Items
);

public record ReceiptItemDto(
    string ProductName,
    string BatchCode,
    string Barcode,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal FinalPrice
);

public record PosScanResultDto(
    bool Success,
    string Message,
    int? ProductId,
    string? ProductName,
    string? Barcode,
    int? BatchId,
    string? BatchCode,
    int? RemainingQuantity,
    decimal Price,
    bool IsDiscounted,
    decimal? DiscountPercent,
    decimal? FinalPrice,
    int? PromotionId,
    string? PromotionName,
    bool IsExpired
);

// Inventory & Expiry
public record InventoryBatchDto(
    int Id,
    string BatchCode,
    int ProductId,
    string ProductName,
    string ProductCode,
    string CategoryName,
    decimal Price,
    int InitialQuantity,
    int RemainingQuantity,
    DateTime ProductionDate,
    DateTime ExpiryDate,
    double HoursUntilExpiry,
    string Status,
    string StatusJapanese
);

public record ExpiryRiskDto(
    int BatchId,
    string BatchCode,
    int ProductId,
    string ProductName,
    string Barcode,
    int RemainingQuantity,
    double HoursUntilExpiry,
    decimal AverageDailySales,
    int EstimatedNormalSalesUntilExpiry,
    int PotentialWasteUnits,
    decimal PotentialWasteCost,
    string RiskLevel,
    string RiskJapanese
);

// AI Recommendations
public record AIRecommendationDto(
    int Id,
    string RecommendationCode,
    int TargetProductId,
    string TargetProductName,
    int TargetBatchId,
    string TargetBatchCode,
    string RecommendationType,
    string RiskLevel,
    string RecommendedAction,
    decimal? RecommendedDiscountPercent,
    decimal? RecommendedComboPrice,
    DateTime StartTime,
    DateTime EndTime,
    int ExpectedSales,
    int ExpectedWasteReduction,
    decimal ExpectedRevenue,
    string Reason,
    string Status,
    DateTime CreatedAt,
    List<AIEvidenceDto> Evidences,
    int? ComboProductId = null,
    string? ComboProductName = null,
    decimal? RecommendedComboSavings = null
);

public record AIEvidenceDto(
    string Key,
    string Value,
    string Description
);

public record ApproveRecommendationRequest(
    string? Notes,
    decimal? CustomDiscountPercent = null,
    decimal? CustomComboPrice = null
);

public record RejectRecommendationRequest(
    string Reason
);

// Promotions
public record PromotionDto(
    int Id,
    string PromotionCode,
    string Name,
    string PromotionType,
    string Status,
    int? TargetProductId,
    string? TargetProductName,
    string? TargetBatchCode,
    decimal? DiscountPercent,
    decimal? ComboPrice,
    DateTime StartTime,
    DateTime EndTime,
    string CreatedVia,
    string CreatedBy,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? AiReasoning,
    DateTime CreatedAt,
    int? ComboProductId = null,
    string? ComboProductName = null,
    decimal? ComboDiscountAmount = null
);

public record CreatePromotionRequest(
    string Name,
    string PromotionType,
    int TargetProductId,
    int? TargetBatchId,
    decimal? DiscountPercent,
    decimal? ComboPrice,
    DateTime StartTime,
    DateTime EndTime,
    string? Reasoning
);

// Chatbot
public record ChatMessageDto(
    string Role, // user, assistant, system
    string Content,
    DateTime Timestamp
);

public record ChatRequest(
    string Message,
    List<ChatMessageDto>? History
);

public record ChatResponse(
    string Reply,
    bool HasPromotionProposal,
    PromotionProposalDto? ProposedPromotion,
    List<AiAgentTraceStepDto>? AgentSteps = null
);

public record PromotionProposalDto(
    string Name,
    string PromotionType,
    int TargetProductId,
    string TargetProductName,
    int? TargetBatchId,
    string? TargetBatchCode,
    decimal DiscountPercent,
    decimal? OriginalPrice,
    decimal? DiscountedPrice,
    DateTime StartTime,
    DateTime EndTime,
    string Reasoning,
    int? PromotionId = null,
    string? Status = "PENDING"
);

// Dashboard & Results
public record DashboardStatsDto(
    int AtRiskProductsCount,
    int CriticalProductsCount,
    decimal PotentialWasteCost,
    int PendingAiRecommendationsCount,
    int ActivePromotionsCount,
    decimal WasteReductionRate,
    decimal RecoveredRevenueTotal,
    int TotalItemsSaved
);

public record PromotionResultDto(
    int Id,
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    string ProductName,
    string BatchCode,
    int InitialStock,
    int StockBeforePromotion,
    int UnitsSold,
    int UnitsRemaining,
    int ExpiredUnits,
    int ExpectedSales,
    int ActualSales,
    int ActualWasteAvoided,
    decimal WasteReductionRate,
    decimal ActualRevenue,
    DateTime EvaluatedAt
);

// AI Multi-Agent Pipeline Trace
public record AiAgentTraceStepDto(
    string AgentKey,
    string AgentName,
    string RoleTitle,
    string Description,
    List<string> Details,
    string Status,
    int DurationMs
);

public record AiPipelineRunResponse(
    bool Success,
    string Message,
    int CreatedCount,
    List<AiAgentTraceStepDto> Steps
);

public record AiPipelineStreamEvent(
    string EventType, // "step" | "complete" | "error"
    AiAgentTraceStepDto? Step = null,
    int? StepIndex = null,
    int? TotalSteps = null,
    int? CreatedCount = null,
    string? Message = null
);



