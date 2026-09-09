using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class PosService : IPosService
{
    private readonly IPromotionRepository _promotionRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserRepository _userRepository;

    public PosService(
        IPromotionRepository promotionRepository,
        IProductRepository productRepository,
        IInventoryService inventoryService,
        IInventoryBatchRepository batchRepository,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IUserRepository userRepository)
    {
        _promotionRepository = promotionRepository;
        _productRepository = productRepository;
        _inventoryService = inventoryService;
        _batchRepository = batchRepository;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
    }

    public async Task<PosRecommendationResponse> GetRecommendationsForCartAsync(
        PosRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // BR-005: ONLY APPROVED promotions active in time window
        var activePromotions = await _promotionRepository.GetActivePromotionsAsync(now, cancellationToken);
        var recommendations = new List<PosRecommendationItemDto>();

        foreach (var promo in activePromotions)
        {
            if (promo.PromotionType == "DIRECT_DISCOUNT" && promo.TargetProductId.HasValue && promo.TargetBatchId.HasValue)
            {
                var targetProduct = promo.TargetProduct;
                var targetBatch = promo.TargetBatch;
                if (targetProduct != null && targetBatch != null)
                {
                    // Filter out expired or sold-out batches
                    if (targetBatch.ExpiryDate <= now || targetBatch.RemainingQuantity <= 0)
                    {
                        continue;
                    }

                    decimal discountPct = promo.DiscountPercent ?? 0m;
                    decimal originalPrice = targetProduct.Price;
                    decimal finalPrice = originalPrice * (1.0m - discountPct / 100.0m);

                    bool isBatchInCart = request.BatchIdsInCart != null && request.BatchIdsInCart.Contains(targetBatch.Id);

                    if (isBatchInCart)
                    {
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: promo.PromotionType,
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            TargetBatchId: targetBatch.Id,
                            TargetBatchCode: targetBatch.BatchCode,
                            ExpiryDate: targetBatch.ExpiryDate,
                            OriginalPrice: originalPrice,
                            DiscountPercent: discountPct,
                            FinalPrice: finalPrice,
                            Message: $"【値引き適用】{targetProduct.Name} ({targetBatch.BatchCode}) が {discountPct:F0}% OFF です！",
                            ActionPrompt: $"{targetProduct.Name} (ロット: {targetBatch.BatchCode}) の値引き価格がレジで適用されています。"
                        ));
                    }
                    else
                    {
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: promo.PromotionType,
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            TargetBatchId: targetBatch.Id,
                            TargetBatchCode: targetBatch.BatchCode,
                            ExpiryDate: targetBatch.ExpiryDate,
                            OriginalPrice: originalPrice,
                            DiscountPercent: discountPct,
                            FinalPrice: finalPrice,
                            Message: $"🏷️ 値引きシール対象：{targetProduct.Name} ({targetBatch.BatchCode}) が {discountPct:F0}% OFF！",
                            ActionPrompt: $"「{targetProduct.Name} の見切り品（ロット: {targetBatch.BatchCode}）が {discountPct:F0}% 引でお買い得です。いかがでしょうか？」"
                        ));
                    }
                }
            }
        }

        return new PosRecommendationResponse(recommendations);
    }

    public async Task<CheckoutResponse> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var txNumber = $"TX-{now:yyyyMMddHHmmss}-{new Random().Next(100, 999)}";

        var staff = await _userRepository.GetStaffUserAsync(cancellationToken);
        int storeId = staff?.StoreId ?? 1;

        var sale = new Sale
        {
            TransactionNumber = txNumber,
            StoreId = storeId,
            StaffId = staff?.Id,
            CustomerId = request.CustomerId,
            PaymentMethod = request.PaymentMethod,
            CreatedAt = now,
            Status = "COMPLETED"
        };

        decimal calculatedSubtotal = 0m;
        decimal calculatedDiscount = 0m;
        var receiptItems = new List<ReceiptItemDto>();

        // STEP 1: Strict Pre-validation Guards (Rules 3, 4, 7, 8, 9, 10)
        foreach (var itemReq in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
            if (product == null)
            {
                return new CheckoutResponse(
                    Success: false,
                    Message: $"指定された商品 (ID: {itemReq.ProductId}) が存在しません。",
                    TransactionNumber: string.Empty,
                    Subtotal: 0,
                    DiscountAmount: 0,
                    TotalAmount: 0,
                    ChangeAmount: 0,
                    CreatedAt: now,
                    Items: new List<ReceiptItemDto>()
                );
            }

            var batch = await _batchRepository.GetByIdAsync(itemReq.BatchId, cancellationToken);
            if (batch == null || batch.ProductId != product.Id)
            {
                return new CheckoutResponse(
                    Success: false,
                    Message: $"商品「{product.Name}」に対応するロット情報 (ID: {itemReq.BatchId}) が見つかりません。",
                    TransactionNumber: string.Empty,
                    Subtotal: 0,
                    DiscountAmount: 0,
                    TotalAmount: 0,
                    ChangeAmount: 0,
                    CreatedAt: now,
                    Items: new List<ReceiptItemDto>()
                );
            }

            // RULE 7: Expired item sales are strictly prohibited! (賞味期限切れ商品の販売禁止)
            if (batch.ExpiryDate <= now)
            {
                return new CheckoutResponse(
                    Success: false,
                    Message: $"【販売不可】商品「{product.Name}」(ロット: {batch.BatchCode}) は賞味期限 ({batch.ExpiryDate:yyyy/MM/dd HH:mm}) が切れています。レジを通すことはできません。",
                    TransactionNumber: string.Empty,
                    Subtotal: 0,
                    DiscountAmount: 0,
                    TotalAmount: 0,
                    ChangeAmount: 0,
                    CreatedAt: now,
                    Items: new List<ReceiptItemDto>()
                );
            }

            // RULE 3, 4, 5, 6, 8, 10: Batch-specific promotion enforcement & anti-fraud check
            if (itemReq.AppliedPromotionId.HasValue)
            {
                var promo = await _promotionRepository.GetByIdAsync(itemReq.AppliedPromotionId.Value, cancellationToken);
                if (promo == null || promo.Status != "APPROVED")
                {
                    return new CheckoutResponse(
                        Success: false,
                        Message: $"適用された値引きプロモーションは承認されていないか、無効です（未承認値引き禁止）。",
                        TransactionNumber: string.Empty,
                        Subtotal: 0,
                        DiscountAmount: 0,
                        TotalAmount: 0,
                        ChangeAmount: 0,
                        CreatedAt: now,
                        Items: new List<ReceiptItemDto>()
                    );
                }

                // Anti-fraud: Promotion target batch must strictly match the item's batch!
                if (promo.TargetBatchId.HasValue && promo.TargetBatchId.Value != batch.Id)
                {
                    return new CheckoutResponse(
                        Success: false,
                        Message: $"【不正適用防止】プロモーション「{promo.Name}」はロット「{promo.TargetBatch?.BatchCode ?? promo.TargetBatchId.ToString()}」専用です。スキャンされたロット「{batch.BatchCode}」には適用できません。",
                        TransactionNumber: string.Empty,
                        Subtotal: 0,
                        DiscountAmount: 0,
                        TotalAmount: 0,
                        ChangeAmount: 0,
                        CreatedAt: now,
                        Items: new List<ReceiptItemDto>()
                    );
                }

                if (promo.StartTime > now || promo.EndTime < now)
                {
                    return new CheckoutResponse(
                        Success: false,
                        Message: $"プロモーション「{promo.Name}」は現在有効期間外です（終了日時: {promo.EndTime:yyyy/MM/dd HH:mm}）。",
                        TransactionNumber: string.Empty,
                        Subtotal: 0,
                        DiscountAmount: 0,
                        TotalAmount: 0,
                        ChangeAmount: 0,
                        CreatedAt: now,
                        Items: new List<ReceiptItemDto>()
                    );
                }
            }

            // Stock limit per specific batch
            if (itemReq.Quantity > batch.RemainingQuantity)
            {
                return new CheckoutResponse(
                    Success: false,
                    Message: $"ロット「{batch.BatchCode}」の在庫が不足しています（ロット残数: {batch.RemainingQuantity}個、購入希望数: {itemReq.Quantity}個）。",
                    TransactionNumber: string.Empty,
                    Subtotal: 0,
                    DiscountAmount: 0,
                    TotalAmount: 0,
                    ChangeAmount: 0,
                    CreatedAt: now,
                    Items: new List<ReceiptItemDto>()
                );
            }
        }

        // STEP 2: Exact Batch Inventory Deduction & Price Calculation
        foreach (var itemReq in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
            if (product == null) continue;

            var batch = await _batchRepository.GetByIdAsync(itemReq.BatchId, cancellationToken);
            if (batch == null) continue;

            // Deduct directly from the specific batch
            batch.RemainingQuantity -= itemReq.Quantity;
            batch.UpdatedAt = now;
            if (batch.RemainingQuantity <= 0)
            {
                batch.RemainingQuantity = 0;
                batch.Status = "SOLD_OUT";
            }
            _batchRepository.Update(batch);

            decimal itemDiscount = 0m;
            if (itemReq.AppliedPromotionId.HasValue)
            {
                var promo = await _promotionRepository.GetByIdAsync(itemReq.AppliedPromotionId.Value, cancellationToken);
                if (promo != null && promo.Status == "APPROVED" && promo.TargetBatchId == batch.Id)
                {
                    decimal pct = promo.DiscountPercent ?? 0m;
                    itemDiscount = Math.Round(product.Price * (pct / 100m) * itemReq.Quantity, 0);
                }
            }

            decimal lineSubtotal = product.Price * itemReq.Quantity;
            decimal lineFinal = lineSubtotal - itemDiscount;

            calculatedSubtotal += lineSubtotal;
            calculatedDiscount += itemDiscount;

            var saleItem = new SaleItem
            {
                Sale = sale,
                ProductId = product.Id,
                BatchId = batch.Id,
                Quantity = itemReq.Quantity,
                UnitPrice = product.Price,
                DiscountAmount = itemDiscount,
                FinalPrice = lineFinal,
                AppliedPromotionId = itemReq.AppliedPromotionId
            };

            sale.Items.Add(saleItem);

            receiptItems.Add(new ReceiptItemDto(
                ProductName: product.Name,
                BatchCode: batch.BatchCode,
                Quantity: itemReq.Quantity,
                UnitPrice: product.Price,
                DiscountAmount: itemDiscount,
                FinalPrice: lineFinal
            ));

            if (request.CustomerId.HasValue)
            {
                await _customerRepository.RecordPurchaseHistoryAsync(
                    request.CustomerId.Value,
                    product.Id,
                    itemReq.Quantity,
                    product.Price,
                    now,
                    cancellationToken
                );
            }
        }

        await _batchRepository.SaveChangesAsync(cancellationToken);

        sale.Subtotal = calculatedSubtotal;
        sale.DiscountAmount = calculatedDiscount;
        sale.TotalAmount = calculatedSubtotal - calculatedDiscount;

        if (request.CustomerId.HasValue)
        {
            int earnedPoints = (int)(sale.TotalAmount / 100m);
            await _customerRepository.AddLoyaltyPointsAsync(request.CustomerId.Value, earnedPoints, cancellationToken);
        }

        await _saleRepository.CreateSaleTransactionAsync(sale, cancellationToken);

        decimal changeAmount = Math.Max(0m, request.ReceivedAmount - sale.TotalAmount);

        return new CheckoutResponse(
            Success: true,
            Message: "お会計が完了しました。ありがとうございます。",
            TransactionNumber: txNumber,
            Subtotal: sale.Subtotal,
            DiscountAmount: sale.DiscountAmount,
            TotalAmount: sale.TotalAmount,
            ChangeAmount: changeAmount,
            CreatedAt: now,
            Items: receiptItems
        );
    }

    public async Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _customerRepository.GetAllCustomersAsync(cancellationToken);
    }
}

