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
    private readonly ILogger<PosService> _logger;

    public PosService(
        IPromotionRepository promotionRepository,
        IProductRepository productRepository,
        IInventoryService inventoryService,
        IInventoryBatchRepository batchRepository,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IUserRepository userRepository,
        ILogger<PosService> logger)
    {
        _promotionRepository = promotionRepository;
        _productRepository = productRepository;
        _inventoryService = inventoryService;
        _batchRepository = batchRepository;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<PosRecommendationResponse> GetRecommendationsForCartAsync(
        PosRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = RetailRescueAI.Backend.Common.AppClock.Now;

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
            else if (promo.PromotionType == "BUNDLE_COMBO" && promo.TargetProductId.HasValue && promo.TargetBatchId.HasValue)
            {
                var targetProduct = promo.TargetProduct;
                var targetBatch = promo.TargetBatch;
                var comboProduct = promo.ComboProduct ?? (promo.ComboProductId.HasValue ? await _productRepository.GetByIdAsync(promo.ComboProductId.Value, cancellationToken) : null);

                if (targetProduct != null && targetBatch != null && comboProduct != null)
                {
                    if (targetBatch.ExpiryDate <= now || targetBatch.RemainingQuantity <= 0)
                    {
                        continue;
                    }

                    bool isMainBatchInCart = request.BatchIdsInCart != null && request.BatchIdsInCart.Contains(targetBatch.Id);
                    bool isMainProductInCart = request.ProductIdsInCart != null && request.ProductIdsInCart.Contains(targetProduct.Id);
                    bool isPartnerInCart = request.ProductIdsInCart != null && request.ProductIdsInCart.Contains(comboProduct.Id);

                    decimal comboPrice = promo.ComboPrice ?? 350m;
                    decimal totalNormal = targetProduct.Price + comboProduct.Price;
                    decimal savings = promo.ComboDiscountAmount ?? (totalNormal - comboPrice);
                    if (savings <= 0) savings = 70m;

                    // Condition 1: Main product/batch is in cart, Partner is NOT yet in cart -> AI UPSELL SUGGESTION!
                    if ((isMainBatchInCart || isMainProductInCart) && !isPartnerInCart)
                    {
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: "BUNDLE_COMBO",
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            TargetBatchId: targetBatch.Id,
                            TargetBatchCode: targetBatch.BatchCode,
                            ExpiryDate: targetBatch.ExpiryDate,
                            OriginalPrice: totalNormal,
                            DiscountPercent: Math.Round(savings / totalNormal * 100, 0),
                            FinalPrice: comboPrice,
                            Message: $"🥪🍵 【AI接客アシスト】{comboProduct.Name} を追加するとセット価格 ¥{comboPrice:N0}（¥{savings:N0}お得！）",
                            ActionPrompt: $"「お客様、ご一緒に『{comboProduct.Name}』はいかがでしょうか？ただいまサンドイッチとセットで通常¥{totalNormal:N0}のところ、¥{comboPrice:N0}（¥{savings:N0}お得）でお買い求めいただけます！」",
                            ComboProductId: comboProduct.Id,
                            ComboProductName: comboProduct.Name,
                            ComboPrice: comboPrice,
                            SavingsAmount: savings,
                            StaffScript: $"「お客様、ご一緒に『{comboProduct.Name}』はいかがでしょうか？ただいまセットで通常¥{totalNormal:N0}のところ、¥{comboPrice:N0}（¥{savings:N0}お得）でお買い求めいただけます！」"
                        ));
                    }
                    // Condition 2: BOTH items in cart -> Combo Activated notification!
                    else if ((isMainBatchInCart || isMainProductInCart) && isPartnerInCart)
                    {
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: "BUNDLE_COMBO_APPLIED",
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            TargetBatchId: targetBatch.Id,
                            TargetBatchCode: targetBatch.BatchCode,
                            ExpiryDate: targetBatch.ExpiryDate,
                            OriginalPrice: totalNormal,
                            DiscountPercent: Math.Round(savings / totalNormal * 100, 0),
                            FinalPrice: comboPrice,
                            Message: $"🎉 【ランチコンボ適用中】{targetProduct.Name}＋{comboProduct.Name} セット割引（¥{savings:N0}引き）が適用されています！",
                            ActionPrompt: "セット割引が自動適用されました。",
                            ComboProductId: comboProduct.Id,
                            ComboProductName: comboProduct.Name,
                            ComboPrice: comboPrice,
                            SavingsAmount: savings,
                            StaffScript: "セット割引が自動適用されました。"
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
        var now = RetailRescueAI.Backend.Common.AppClock.Now;
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

                // Anti-fraud: Promotion target batch must strictly match the item's batch (or be valid combo partner)!
                if (promo.TargetBatchId.HasValue && promo.TargetBatchId.Value != batch.Id && promo.ComboProductId != product.Id)
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
            int? effectivePromoId = itemReq.AppliedPromotionId;
            if (!effectivePromoId.HasValue)
            {
                var activePromos = await _promotionRepository.GetActivePromotionsAsync(now, cancellationToken);

                // 1) Check BUNDLE_COMBO where this batch is target and partner product is also in the checkout request!
                var comboPromo = activePromos.FirstOrDefault(p =>
                    p.PromotionType == "BUNDLE_COMBO" &&
                    p.TargetBatchId == batch.Id &&
                    p.Status == "APPROVED" &&
                    p.StartTime <= now &&
                    p.EndTime >= now &&
                    p.ComboProductId.HasValue &&
                    request.Items.Any(i => i.ProductId == p.ComboProductId.Value));

                if (comboPromo != null)
                {
                    effectivePromoId = comboPromo.Id;
                }
                else
                {
                    // 2) Check DIRECT_DISCOUNT for this batch
                    var batchPromo = activePromos.FirstOrDefault(p =>
                        p.PromotionType == "DIRECT_DISCOUNT" &&
                        p.TargetBatchId == batch.Id &&
                        p.Status == "APPROVED" &&
                        p.StartTime <= now &&
                        p.EndTime >= now);

                    if (batchPromo != null)
                    {
                        effectivePromoId = batchPromo.Id;
                    }
                }
            }

            if (effectivePromoId.HasValue)
            {
                var promo = await _promotionRepository.GetByIdAsync(effectivePromoId.Value, cancellationToken);
                if (promo != null && promo.Status == "APPROVED")
                {
                    if (promo.PromotionType == "BUNDLE_COMBO" && promo.TargetBatchId == batch.Id)
                    {
                        // Apply combo savings on this combo line
                        decimal savings = promo.ComboDiscountAmount ?? 70m;
                        var partnerItem = request.Items.FirstOrDefault(i => i.ProductId == promo.ComboProductId);
                        int comboQuantity = partnerItem != null ? Math.Min(itemReq.Quantity, partnerItem.Quantity) : itemReq.Quantity;
                        itemDiscount = savings * comboQuantity;
                    }
                    else if (promo.TargetBatchId == batch.Id && promo.DiscountPercent.HasValue)
                    {
                        decimal pct = promo.DiscountPercent.Value;
                        itemDiscount = Math.Round(product.Price * (pct / 100m) * itemReq.Quantity, 0);
                    }
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

            _logger.LogInformation(
                "[PosService] Scanned Barcode '{Barcode}': Deducted {Quantity} units directly from Lot {BatchCode} (ID: {BatchId}) for Product '{ProductName}'. Lot remaining stock: {Remaining}.",
                itemReq.ScannedBarcode ?? product.Barcode,
                itemReq.Quantity,
                batch.BatchCode,
                batch.Id,
                product.Name,
                batch.RemainingQuantity);

            receiptItems.Add(new ReceiptItemDto(
                ProductName: product.Name,
                BatchCode: batch.BatchCode,
                Barcode: itemReq.ScannedBarcode ?? product.Barcode,
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

    public async Task<PosScanResultDto> ScanBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return new PosScanResultDto(
                Success: false,
                Message: "バーコードが入力されていません。",
                ProductId: null,
                ProductName: null,
                Barcode: null,
                BatchId: null,
                BatchCode: null,
                RemainingQuantity: null,
                Price: 0,
                IsDiscounted: false,
                DiscountPercent: null,
                FinalPrice: null,
                PromotionId: null,
                PromotionName: null,
                IsExpired: false
            );
        }

        var cleanCode = barcode.Trim();
        var now = RetailRescueAI.Backend.Common.AppClock.Now;

        // 1. Direct BatchCode Match (e.g. BATCH-SAND-001, BATCH-SAND-002)
        var batchByCode = await _batchRepository.GetByBatchCodeAsync(cleanCode, cancellationToken);
        if (batchByCode != null)
        {
            var product = batchByCode.Product ?? await _productRepository.GetByIdAsync(batchByCode.ProductId, cancellationToken);
            return await BuildScanResultAsync(batchByCode, product, cleanCode, now, cancellationToken);
        }

        // 2. Discount Sticker Match (e.g. STICKER-BATCH-SAND-001, STICKER-SAND-001)
        if (cleanCode.StartsWith("STICKER-", StringComparison.OrdinalIgnoreCase) || cleanCode.StartsWith("DISCOUNT-", StringComparison.OrdinalIgnoreCase))
        {
            var rawSuffix = cleanCode.Replace("STICKER-", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("DISCOUNT-", "", StringComparison.OrdinalIgnoreCase).Trim();

            var allBatches = await _batchRepository.GetAllBatchesWithProductAsync(cancellationToken);
            var activePromos = await _promotionRepository.GetActivePromotionsAsync(now, cancellationToken);
            var discountedBatchIds = activePromos.Where(p => p.Status == "APPROVED" && p.TargetBatchId.HasValue)
                                                .Select(p => p.TargetBatchId!.Value)
                                                .ToHashSet();

            // First: Try exact batch code match with or without BATCH- prefix
            var matchedBatch = allBatches.FirstOrDefault(b => 
                b.BatchCode.Equals(rawSuffix, StringComparison.OrdinalIgnoreCase) ||
                b.BatchCode.Equals($"BATCH-{rawSuffix}", StringComparison.OrdinalIgnoreCase) ||
                cleanCode.Contains(b.BatchCode, StringComparison.OrdinalIgnoreCase));

            // Second: If matched by product code (e.g. STICKER-SAND-001), find the active discounted non-expired batch for that product!
            if (matchedBatch == null)
            {
                matchedBatch = allBatches
                    .Where(b => b.ExpiryDate > now && b.Product != null &&
                               (b.Product.ProductCode.Equals(rawSuffix, StringComparison.OrdinalIgnoreCase) ||
                                cleanCode.Contains(b.Product.ProductCode, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(b => discountedBatchIds.Contains(b.Id)) // prefer discounted batch
                    .ThenBy(b => b.ExpiryDate) // then nearest expiry
                    .FirstOrDefault();
            }

            if (matchedBatch != null)
            {
                return await BuildScanResultAsync(matchedBatch, matchedBatch.Product, cleanCode, now, cancellationToken);
            }
        }

        // 3. Product JAN Barcode or Product Code Match
        var allProducts = await _productRepository.GetAllAsync(cancellationToken);
        var matchedProduct = allProducts.FirstOrDefault(p =>
            p.Barcode.Equals(cleanCode, StringComparison.OrdinalIgnoreCase) ||
            p.ProductCode.Equals(cleanCode, StringComparison.OrdinalIgnoreCase));

        if (matchedProduct != null)
        {
            var productBatches = await _batchRepository.GetAvailableBatchesForProductFefoAsync(matchedProduct.Id, cancellationToken);
            var nonExpiredBatches = productBatches.Where(b => b.ExpiryDate > now).ToList();

            // When standard JAN is scanned, prefer the regular (fresh, non-discounted) batch, or earliest valid batch
            var regularBatch = nonExpiredBatches.FirstOrDefault(b => b.Status == "AVAILABLE") ?? nonExpiredBatches.FirstOrDefault();

            if (regularBatch != null)
            {
                return await BuildScanResultAsync(regularBatch, matchedProduct, cleanCode, now, cancellationToken);
            }

            return new PosScanResultDto(
                Success: false,
                Message: $"商品「{matchedProduct.Name}」は現在有効なロット在庫がありません（完売または賞味期限切れ）。",
                ProductId: matchedProduct.Id,
                ProductName: matchedProduct.Name,
                Barcode: matchedProduct.Barcode,
                BatchId: null,
                BatchCode: null,
                RemainingQuantity: 0,
                Price: matchedProduct.Price,
                IsDiscounted: false,
                DiscountPercent: null,
                FinalPrice: matchedProduct.Price,
                PromotionId: null,
                PromotionName: null,
                IsExpired: true
            );
        }

        return new PosScanResultDto(
            Success: false,
            Message: $"バーコード「{cleanCode}」に一致する商品・ロットが見つかりませんでした。",
            ProductId: null,
            ProductName: null,
            Barcode: cleanCode,
            BatchId: null,
            BatchCode: null,
            RemainingQuantity: null,
            Price: 0,
            IsDiscounted: false,
            DiscountPercent: null,
            FinalPrice: null,
            PromotionId: null,
            PromotionName: null,
            IsExpired: false
        );
    }

    private async Task<PosScanResultDto> BuildScanResultAsync(
        InventoryBatch batch,
        Product? product,
        string scannedCode,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var prod = product ?? batch.Product;
        var price = prod?.Price ?? 0m;
        bool isExpired = batch.ExpiryDate <= now;

        var activePromos = await _promotionRepository.GetActivePromotionsAsync(now, cancellationToken);
        var batchPromo = activePromos.FirstOrDefault(p => p.TargetBatchId == batch.Id && p.Status == "APPROVED");

        bool isDiscounted = batchPromo != null && !isExpired;
        decimal? discountPct = isDiscounted ? batchPromo?.DiscountPercent : null;
        decimal? finalPrice = isDiscounted && discountPct.HasValue
            ? Math.Round(price * (1m - discountPct.Value / 100m), 0)
            : price;

        string message = isExpired
            ? $"⛔ 【販売不可】ロット「{batch.BatchCode}」は賞味期限が切れています！店頭から撤去してください（Rule 7: 期限切れ販売禁止）。"
            : isDiscounted
                ? $"✅ 【値引きシール適用】「{prod?.Name}」(ロット: {batch.BatchCode}) が {discountPct}% OFF で読み取られました。"
                : $"✅ 【スキャン完了】「{prod?.Name}」(ロット: {batch.BatchCode}) を読み取りました。";

        return new PosScanResultDto(
            Success: !isExpired,
            Message: message,
            ProductId: prod?.Id,
            ProductName: prod?.Name,
            Barcode: scannedCode,
            BatchId: batch.Id,
            BatchCode: batch.BatchCode,
            RemainingQuantity: batch.RemainingQuantity,
            Price: price,
            IsDiscounted: isDiscounted,
            DiscountPercent: discountPct,
            FinalPrice: finalPrice,
            PromotionId: batchPromo?.Id,
            PromotionName: batchPromo?.Name,
            IsExpired: isExpired
        );
    }
}

