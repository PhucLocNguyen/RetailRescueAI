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
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserRepository _userRepository;

    public PosService(
        IPromotionRepository promotionRepository,
        IProductRepository productRepository,
        IInventoryService inventoryService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IUserRepository userRepository)
    {
        _promotionRepository = promotionRepository;
        _productRepository = productRepository;
        _inventoryService = inventoryService;
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
            if (promo.PromotionType == "DIRECT_DISCOUNT" && promo.TargetProductId.HasValue)
            {
                var targetProduct = promo.TargetProduct;
                if (targetProduct != null)
                {
                    decimal discountPct = promo.DiscountPercent ?? 0m;
                    decimal originalPrice = targetProduct.Price;
                    decimal finalPrice = originalPrice * (1.0m - discountPct / 100.0m);

                    if (request.ProductIdsInCart.Contains(targetProduct.Id))
                    {
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: promo.PromotionType,
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            OriginalPrice: originalPrice,
                            DiscountPercent: discountPct,
                            FinalPrice: finalPrice,
                            Message: $"【直前割適用】{targetProduct.Name} が {discountPct:F0}% OFF です！",
                            ActionPrompt: $"{targetProduct.Name} の割引価格がレジで適用されています。"
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
                            OriginalPrice: originalPrice,
                            DiscountPercent: discountPct,
                            FinalPrice: finalPrice,
                            Message: $"💡 本日の特売：{targetProduct.Name} が {discountPct:F0}% OFF！",
                            ActionPrompt: $"「{targetProduct.Name}が現在20%引きでお買い得です。いかがでしょうか？」"
                        ));
                    }
                }
            }
            else if (promo.PromotionType == "BUY_X_GET_DISCOUNT")
            {
                var condition = promo.Conditions.FirstOrDefault(c => c.ConditionType == "REQUIRED_PRODUCT");
                if (condition != null && condition.RequiredProductId.HasValue && promo.TargetProductId.HasValue)
                {
                    var hasRequiredProduct = request.ProductIdsInCart.Contains(condition.RequiredProductId.Value);
                    if (hasRequiredProduct)
                    {
                        var targetProduct = promo.TargetProduct;
                        if (targetProduct != null)
                        {
                            decimal discountPct = promo.DiscountPercent ?? 20m;
                            decimal originalPrice = targetProduct.Price;
                            decimal finalPrice = originalPrice * (1.0m - discountPct / 100.0m);

                            recommendations.Add(new PosRecommendationItemDto(
                                PromotionId: promo.Id,
                                PromotionCode: promo.PromotionCode,
                                PromotionName: promo.Name,
                                PromotionType: promo.PromotionType,
                                TargetProductId: targetProduct.Id,
                                TargetProductName: targetProduct.Name,
                                OriginalPrice: originalPrice,
                                DiscountPercent: discountPct,
                                FinalPrice: finalPrice,
                                Message: $"💡 お弁当＋サラダ割引：{targetProduct.Name} を追加すると {discountPct:F0}% OFF！",
                                ActionPrompt: $"「お弁当をお買い上げのお客様に、{targetProduct.Name}が20%OFFになります。ご一緒にいかがでしょうか？」"
                            ));
                        }
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

        foreach (var itemReq in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
            if (product == null) continue;

            // Deduct stock via FEFO
            var primaryDeductedBatch = await _inventoryService.DeductBatchInventoryFefoAsync(product.Id, itemReq.Quantity, cancellationToken);

            decimal itemDiscount = 0m;
            if (itemReq.AppliedPromotionId.HasValue)
            {
                var promo = await _promotionRepository.GetByIdAsync(itemReq.AppliedPromotionId.Value, cancellationToken);
                if (promo != null && promo.Status == "APPROVED")
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
                BatchId = primaryDeductedBatch?.Id,
                Quantity = itemReq.Quantity,
                UnitPrice = product.Price,
                DiscountAmount = itemDiscount,
                FinalPrice = lineFinal,
                AppliedPromotionId = itemReq.AppliedPromotionId
            };

            sale.Items.Add(saleItem);

            receiptItems.Add(new ReceiptItemDto(
                ProductName: product.Name,
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

