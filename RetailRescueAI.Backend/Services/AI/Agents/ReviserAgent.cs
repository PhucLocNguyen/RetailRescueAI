using System.Text.Json;
using Microsoft.SemanticKernel;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.AI.Plugins;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public record ValidatedProposalResult(
    PromotionProposal Proposal,
    bool IsValid,
    List<string> ValidationMessages
);

/// <summary>
/// Semantic Kernel Agent responsible for validating retail business rules
/// (BR-003, BR-006, margin >= 15%) via SafetyGuardrailPlugin.
/// </summary>
public class ReviserAgent
{
    private readonly Kernel _kernel;
    private readonly SafetyGuardrailPlugin _safetyPlugin;
    private readonly ILogger<ReviserAgent> _logger;

    public string Name => "SafetyGuardrailAgent";
    public string RoleTitle => "安全制約検証エージェント (Semantic Kernel)";

    public ReviserAgent(Kernel kernel, SafetyGuardrailPlugin safetyPlugin, ILogger<ReviserAgent> logger)
    {
        _kernel = kernel;
        _safetyPlugin = safetyPlugin;
        _logger = logger;
    }

    public List<ValidatedProposalResult> ValidateProposals(List<PromotionProposal> proposals)
    {
        _logger.LogInformation("[{Agent}] Validating {Count} proposals against retail guardrails via Semantic Kernel...", Name, proposals.Count);

        var validated = new List<ValidatedProposalResult>();

        foreach (var proposal in proposals)
        {
            var batch = proposal.TargetBatch;
            var product = batch.Product!;

            decimal discount = proposal.DiscountPercent ?? 17.0m;
            decimal price = proposal.ComboPrice ?? product.Price;
            decimal cost = (proposal.ComboProduct != null)
                ? (product.CostPrice + proposal.ComboProduct.CostPrice)
                : product.CostPrice;

            string validationJson = _safetyPlugin.ValidatePromotionSafety(
                discount,
                price,
                cost,
                proposal.EndTime.ToString("o"),
                batch.ExpiryDate.ToString("o"),
                batch.RemainingQuantity,
                product.MaxDiscountPercent
            );

            bool isValid = true;
            var messages = new List<string>();

            try
            {
                using var doc = JsonDocument.Parse(validationJson);
                isValid = doc.RootElement.GetProperty("isValid").GetBoolean();
                foreach (var el in doc.RootElement.GetProperty("messages").EnumerateArray())
                {
                    messages.Add(el.GetString() ?? "");
                }
            }
            catch
            {
                // Fallback direct checks
                if (proposal.EndTime > batch.ExpiryDate)
                {
                    isValid = false;
                    messages.Add($"[BR-003 違反] 終了日時が賞味期限を超えています。");
                }
                if (batch.RemainingQuantity <= 0)
                {
                    isValid = false;
                    messages.Add($"[BR-006 違反] 有効残在庫数が0以下です。");
                }
            }

            validated.Add(new ValidatedProposalResult(proposal, isValid, messages));
        }

        return validated;
    }
}
