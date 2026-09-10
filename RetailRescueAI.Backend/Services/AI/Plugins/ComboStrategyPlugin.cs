using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace RetailRescueAI.Backend.Services.AI.Plugins;

public class ComboStrategyPlugin
{
    private readonly ILogger<ComboStrategyPlugin> _logger;

    public ComboStrategyPlugin(ILogger<ComboStrategyPlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction, Description("Evaluates meal combo bundle economics: normal total, combo special price, customer savings, combined gross margin, and cashier speech script")]
    public string EvaluateMealCombo(
        [Description("Main near-expiry product name")] string mainProductName,
        [Description("Main product price in JPY")] decimal mainPrice,
        [Description("Main product cost price in JPY")] decimal mainCost,
        [Description("Partner product name (e.g. green tea)")] string partnerProductName,
        [Description("Partner product price in JPY")] decimal partnerPrice,
        [Description("Partner product cost price in JPY")] decimal partnerCost,
        [Description("Proposed combo bundle price in JPY")] decimal proposedComboPrice)
    {
        decimal normalTotal = mainPrice + partnerPrice;
        decimal totalCost = mainCost + partnerCost;
        decimal savings = Math.Max(0, normalTotal - proposedComboPrice);
        decimal profit = proposedComboPrice - totalCost;
        decimal marginPct = proposedComboPrice > 0 ? Math.Round((profit / proposedComboPrice) * 100m, 1) : 0m;
        bool isMarginSafe = marginPct >= 15.0m;

        string staffScript = $"「お客様、ご一緒に『{partnerProductName}』はいかがでしょうか？ただいまセットで通常¥{normalTotal:N0}のところ、¥{proposedComboPrice:N0}（¥{savings:N0}お得）でお買い求めいただけます！」";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            normalTotal = normalTotal,
            comboPrice = proposedComboPrice,
            customerSavings = savings,
            totalCost = totalCost,
            grossProfit = profit,
            grossProfitMargin = marginPct,
            isMarginSafe = isMarginSafe,
            staffScript = staffScript
        });

        _logger.LogInformation("[ComboStrategyPlugin] Evaluated combo: {Main} + {Partner} = ¥{Combo} (Margin: {Margin}%, Safe: {Safe})",
            mainProductName, partnerProductName, proposedComboPrice, marginPct, isMarginSafe);

        return result;
    }
}

