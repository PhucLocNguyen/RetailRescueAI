using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly AppDbContext _dbContext;

    public InventoryService(
        IInventoryBatchRepository batchRepository,
        ISaleRepository saleRepository,
        AppDbContext dbContext)
    {
        _batchRepository = batchRepository;
        _saleRepository = saleRepository;
        _dbContext = dbContext;
    }

    public async Task<List<InventoryBatchDto>> GetBatchesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var batches = await _batchRepository.GetAllBatchesWithProductAsync(cancellationToken);

        return batches.Select(b =>
        {
            var hours = (b.ExpiryDate - now).TotalHours;

            string statusJp = b.Status switch
            {
                "CRITICAL" => "🔴 危機的（即時対応要）",
                "AT_RISK" => "🟠 期限間近（注意）",
                "AVAILABLE" => "🟢 正常",
                "EXPIRED" => "⚫ 期限切れ",
                "SOLD_OUT" => "⚪ 完売",
                _ => b.Status
            };

            return new InventoryBatchDto(
                b.Id,
                b.BatchCode,
                b.ProductId,
                b.Product?.Name ?? "不明",
                b.Product?.ProductCode ?? "",
                b.Product?.Category?.Name ?? "一般",
                b.Product?.Price ?? 0m,
                b.InitialQuantity,
                b.RemainingQuantity,
                b.ProductionDate,
                b.ExpiryDate,
                Math.Round(hours, 1),
                b.Status,
                statusJp
            );
        }).ToList();
    }

    public async Task<List<ExpiryRiskDto>> GetExpiryRiskAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activeBatches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);
        var recentSaleItems = await _saleRepository.GetRecentSaleItemsAsync(now.AddDays(-7), cancellationToken);

        var dtos = new List<ExpiryRiskDto>();

        foreach (var b in activeBatches)
        {
            var hours = (b.ExpiryDate - now).TotalHours;
            var product = b.Product!;

            var soldPast7Days = recentSaleItems
                .Where(si => si.ProductId == product.Id)
                .Sum(si => si.Quantity);

            decimal avgDailySales = soldPast7Days > 0 ? Math.Round((decimal)soldPast7Days / 7.0m, 1) : 10.0m;
            decimal daysLeft = (decimal)Math.Max(0, hours) / 24.0m;
            int estNormalSales = (int)Math.Floor(avgDailySales * daysLeft);
            int potentialWaste = Math.Max(0, b.RemainingQuantity - estNormalSales);
            decimal potentialCost = potentialWaste * product.Price;

            string riskJp = b.Status switch
            {
                "CRITICAL" => "危機的（High Risk）",
                "AT_RISK" => "要警戒（Medium Risk）",
                _ => "低リスク（Low Risk）"
            };

            dtos.Add(new ExpiryRiskDto(
                b.Id,
                b.BatchCode,
                product.Id,
                product.Name,
                product.Barcode,
                b.RemainingQuantity,
                Math.Round(hours, 1),
                avgDailySales,
                estNormalSales,
                potentialWaste,
                potentialCost,
                b.Status,
                riskJp
            ));
        }

        return dtos;
    }

    public async Task<InventoryBatch?> DeductBatchInventoryFefoAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var batches = await _batchRepository.GetAvailableBatchesForProductFefoAsync(productId, cancellationToken);

        int remainingToDeduct = quantity;
        InventoryBatch? primaryBatch = null;

        foreach (var batch in batches)
        {
            if (remainingToDeduct <= 0) break;

            int deduct = Math.Min(batch.RemainingQuantity, remainingToDeduct);
            batch.RemainingQuantity -= deduct;
            batch.UpdatedAt = now;
            remainingToDeduct -= deduct;

            if (batch.RemainingQuantity == 0)
            {
                batch.Status = "SOLD_OUT";
            }

            primaryBatch ??= batch;
        }

        await _batchRepository.SaveChangesAsync(cancellationToken);
        return primaryBatch;
    }

    public async Task ResetDemoDataAsync(CancellationToken cancellationToken = default)
    {
        await DbInitializer.ResetDemoDataAsync(_dbContext);
    }
}
