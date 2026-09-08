using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Services;

public class InventoryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(AppDbContext context, ILogger<InventoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CheckoutResponse> ProcessCheckoutAsync(
        int? staffId,
        int storeId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var txNumber = $"TX-{now:yyyyMMddHHmmss}-{new Random().Next(100, 999)}";

        var sale = new Sale
        {
            TransactionNumber = txNumber,
            StoreId = storeId,
            StaffId = staffId,
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
            var product = await _context.Products.FindAsync(new object[] { itemReq.ProductId }, cancellationToken);
            if (product == null) continue;

            // Deduct from batches with earliest expiry first (FEFO: First Expired, First Out)
            var availableBatches = await _context.InventoryBatches
                .Where(b => b.ProductId == product.Id && b.RemainingQuantity > 0)
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync(cancellationToken);

            int remainingToDeduct = itemReq.Quantity;
            InventoryBatch? primaryDeductedBatch = null;

            foreach (var batch in availableBatches)
            {
                if (remainingToDeduct <= 0) break;

                int deductAmount = Math.Min(batch.RemainingQuantity, remainingToDeduct);
                batch.RemainingQuantity -= deductAmount;
                batch.UpdatedAt = now;
                remainingToDeduct -= deductAmount;

                if (batch.RemainingQuantity == 0)
                {
                    batch.Status = "SOLD_OUT";
                }

                primaryDeductedBatch ??= batch;
            }

            // Check if promotion applied
            decimal itemDiscount = 0m;
            if (itemReq.AppliedPromotionId.HasValue)
            {
                var promo = await _context.Promotions.FindAsync(new object[] { itemReq.AppliedPromotionId.Value }, cancellationToken);
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

            // Customer history
            if (request.CustomerId.HasValue)
            {
                _context.CustomerPurchaseHistories.Add(new CustomerPurchaseHistory
                {
                    CustomerId = request.CustomerId.Value,
                    ProductId = product.Id,
                    Quantity = itemReq.Quantity,
                    UnitPrice = product.Price,
                    PurchaseDate = now
                });
            }
        }

        sale.Subtotal = calculatedSubtotal;
        sale.DiscountAmount = calculatedDiscount;
        sale.TotalAmount = calculatedSubtotal - calculatedDiscount;

        // Customer loyalty points: 1 pt per 100 yen
        if (request.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(new object[] { request.CustomerId.Value }, cancellationToken);
            if (customer != null)
            {
                customer.Points += (int)(sale.TotalAmount / 100m);
            }
        }

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

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
}

