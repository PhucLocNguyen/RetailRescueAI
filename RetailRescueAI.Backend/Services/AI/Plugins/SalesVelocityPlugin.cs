using System.ComponentModel;
using Microsoft.SemanticKernel;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Services.AI.Plugins;

public class SalesVelocityPlugin
{
    private readonly ISaleRepository _saleRepository;
    private readonly ILogger<SalesVelocityPlugin> _logger;

    public SalesVelocityPlugin(ISaleRepository saleRepository, ILogger<SalesVelocityPlugin> logger)
    {
        _saleRepository = saleRepository;
        _logger = logger;
    }

    [KernelFunction, Description("Calculates average daily sales velocity of a product over past N days")]
    public async Task<decimal> CalculateDailySalesVelocityAsync(
        [Description("Product database ID")] int productId,
        [Description("Days lookback window (default 7)")] int days = 7,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(7).AddDays(-Math.Max(1, days));
        var since = RetailRescueAI.Backend.Common.AppClock.Now.AddDays(-Math.Max(1, days));
        var recentItems = await _saleRepository.GetRecentSaleItemsAsync(since, cancellationToken);
        var productSoldUnits = recentItems.Where(si => si.ProductId == productId).Sum(si => si.Quantity);

        decimal avg = productSoldUnits > 0 ? Math.Round((decimal)productSoldUnits / (decimal)days, 1) : 10.0m;
        _logger.LogInformation("[SalesVelocityPlugin] Product {ProductId} velocity: {Avg} units/day", productId, avg);
        return avg;
    }

    [KernelFunction, Description("Forecasts potential waste units and financial loss before expiration based on sales velocity")]
    public string ForecastClearanceAndWaste(
        [Description("Remaining units in stock")] int remainingQuantity,
        [Description("Average daily sales units")] decimal avgDailySales,
        [Description("Remaining hours until expiry")] double hoursUntilExpiry,
        [Description("Retail unit price in JPY")] decimal unitPrice)
    {
        var daysRemaining = (decimal)Math.Max(0, hoursUntilExpiry) / 24.0m;
        var estimatedSales = (int)Math.Floor(avgDailySales * daysRemaining);
        var wasteUnits = Math.Max(0, remainingQuantity - estimatedSales);
        var financialLoss = wasteUnits * unitPrice;

        return $"{{\"estimatedNormalSales\":{estimatedSales},\"potentialWasteUnits\":{wasteUnits},\"potentialFinancialLoss\":{financialLoss}}}";
    }
}

