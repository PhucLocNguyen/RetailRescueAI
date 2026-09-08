using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("batches")]
    public async Task<ActionResult<List<InventoryBatchDto>>> GetBatches()
    {
        var now = DateTime.UtcNow;

        var batches = await _context.InventoryBatches
            .Include(b => b.Product)
            .ThenInclude(p => p!.Category)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();

        var dtos = batches.Select(b =>
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

        return Ok(dtos);
    }

    [HttpGet("expiry-risk")]
    public async Task<ActionResult<List<ExpiryRiskDto>>> GetExpiryRiskAnalysis()
    {
        var now = DateTime.UtcNow;

        var urgentBatches = await _context.InventoryBatches
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();

        var sevenDaysAgo = now.AddDays(-7);
        var recentSaleItems = await _context.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.Sale!.CreatedAt >= sevenDaysAgo && si.Sale.Status == "COMPLETED")
            .ToListAsync();

        var dtos = new List<ExpiryRiskDto>();

        foreach (var b in urgentBatches)
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

        return Ok(dtos);
    }
}

