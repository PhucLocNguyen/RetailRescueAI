using Microsoft.AspNetCore.SignalR;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Hubs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.AI.Agents;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class AiRecommendationService : IAiRecommendationService
{
    private readonly IAiRecommendationRepository _recommendationRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly OrchestratorAgent _orchestratorAgent;
    private readonly IHubContext<PromotionHub> _hubContext;

    public AiRecommendationService(
        IAiRecommendationRepository recommendationRepository,
        IPromotionRepository promotionRepository,
        OrchestratorAgent orchestratorAgent,
        IHubContext<PromotionHub> hubContext)
    {
        _recommendationRepository = recommendationRepository;
        _promotionRepository = promotionRepository;
        _orchestratorAgent = orchestratorAgent;
        _hubContext = hubContext;
    }

    public async Task<List<AIRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        var recs = await _recommendationRepository.GetAllWithDetailsAsync(cancellationToken);

        return recs.Select(r => new AIRecommendationDto(
            r.Id,
            r.RecommendationCode,
            r.TargetProductId,
            r.TargetProduct?.Name ?? "不明商品",
            r.TargetBatchId,
            r.TargetBatch?.BatchCode ?? "不明ロット",
            r.RecommendationType,
            r.RiskLevel,
            r.RecommendedAction,
            r.RecommendedDiscountPercent,
            r.RecommendedComboPrice,
            r.StartTime,
            r.EndTime,
            r.ExpectedSales,
            r.ExpectedWasteReduction,
            r.ExpectedRevenue,
            r.Reason,
            r.Status,
            r.CreatedAt,
            r.Evidences.Select(e => new AIEvidenceDto(e.EvidenceKey, e.EvidenceValue, e.Description)).ToList(),
            r.ComboProductId,
            r.ComboProductName ?? r.ComboProduct?.Name,
            r.RecommendedComboSavings
        )).ToList();
    }

    public async Task<(bool Success, string Message, int? PromotionId)> ApproveRecommendationAsync(
        int id,
        ApproveRecommendationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var rec = await _recommendationRepository.GetWithDetailsAsync(id, cancellationToken);
        if (rec == null) return (false, "AI提案が見つかりません。", null);

        var now = RetailRescueAI.Backend.Common.AppClock.Now;
        rec.Status = "APPROVED";
        rec.ReviewedAt = now;
        rec.ReviewedBy = "佐藤 店長 (Manager)";

        bool isCombo = rec.RecommendationType == "BUNDLE_COMBO";

        // Support Manager adjusting discount % or combo price
        decimal finalDiscount = (request?.CustomDiscountPercent.HasValue == true && request.CustomDiscountPercent.Value > 0)
            ? request.CustomDiscountPercent.Value
            : (rec.RecommendedDiscountPercent ?? 20m);

        decimal? finalComboPrice = request?.CustomComboPrice ?? rec.RecommendedComboPrice;
        decimal? finalComboSavings = rec.RecommendedComboSavings;

        if (isCombo)
        {
            rec.RecommendedComboPrice = finalComboPrice;
            if (request?.CustomComboPrice.HasValue == true)
            {
                rec.RecommendedAction = $"【ランチコンボ】たまごサンド＋宇治緑茶 セットで ¥{finalComboPrice:N0}（店長指定）";
            }
        }
        else
        {
            rec.RecommendedDiscountPercent = finalDiscount;
            if (request?.CustomDiscountPercent.HasValue == true)
            {
                rec.RecommendedAction = $"{rec.TargetProduct?.Name ?? "対象商品"} {finalDiscount:F0}% OFF（店長指定割）";
            }
        }

        var promo = new Promotion
        {
            PromotionCode = $"PROMO-{now:yyyyMMddHHmmss}",
            Name = rec.RecommendedAction,
            PromotionType = rec.RecommendationType,
            Status = "APPROVED",
            TargetProductId = rec.TargetProductId,
            TargetBatchId = rec.TargetBatchId,
            DiscountPercent = isCombo ? null : finalDiscount,
            ComboProductId = rec.ComboProductId,
            ComboPrice = finalComboPrice,
            ComboDiscountAmount = finalComboSavings,
            StartTime = rec.StartTime,
            EndTime = rec.EndTime,
            CreatedVia = "AI_AGENT",
            CreatedBy = "OrchestratorAgent",
            ApprovedBy = "佐藤 店長 (Manager)",
            ApprovedAt = now,
            AiReasoning = rec.Reason,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _promotionRepository.AddAsync(promo, cancellationToken);
        await _promotionRepository.SaveChangesAsync(cancellationToken);

        rec.CreatedPromotionId = promo.Id;
        _recommendationRepository.Update(rec);
        await _recommendationRepository.SaveChangesAsync(cancellationToken);

        // Broadcast Real-time event to POS screens via SignalR
        try
        {
            string broadcastMessage = isCombo
                ? $"🍱 【ランチコンボ承認】{rec.TargetProduct?.Name}＋{rec.ComboProductName ?? "ドリンク"} セットで ¥{finalComboPrice:N0}（¥{finalComboSavings:N0}引き）が承認されました！"
                : $"🏷️ 【値引き承認】{rec.TargetProduct?.Name} (ロット: {rec.TargetBatch?.BatchCode ?? promo.TargetBatchId.ToString()}) が {promo.DiscountPercent:F0}% OFF に承認されました！";

            await _hubContext.Clients.All.SendAsync("PromotionApproved", new
            {
                promotionId = promo.Id,
                promotionCode = promo.PromotionCode,
                promotionName = promo.Name,
                promotionType = promo.PromotionType,
                targetProductId = promo.TargetProductId,
                targetProductName = rec.TargetProduct?.Name,
                targetBatchId = promo.TargetBatchId,
                targetBatchCode = rec.TargetBatch?.BatchCode,
                discountPercent = promo.DiscountPercent,
                comboProductId = promo.ComboProductId,
                comboProductName = rec.ComboProductName ?? rec.ComboProduct?.Name,
                comboPrice = promo.ComboPrice,
                comboSavings = promo.ComboDiscountAmount,
                startTime = promo.StartTime,
                endTime = promo.EndTime,
                message = broadcastMessage
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SignalR] Broadcast error: {ex.Message}");
        }

        return (true, $"提案「{rec.RecommendedAction}」を承認しました。プロモーションがレジで有効化されました。", promo.Id);
    }

    public async Task<bool> RejectRecommendationAsync(int id, RejectRecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var rec = await _recommendationRepository.GetByIdAsync(id, cancellationToken);
        if (rec == null) return false;

        rec.Status = "REJECTED";
        rec.ReviewedAt = RetailRescueAI.Backend.Common.AppClock.Now;
        rec.ReviewedBy = "佐藤 店長 (Manager)";

        _recommendationRepository.Update(rec);
        await _recommendationRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AiPipelineRunResponse> RunManualPipelineAsync(CancellationToken cancellationToken = default)
    {
        var (created, steps) = await _orchestratorAgent.RunFullPipelineWithTraceAsync(cancellationToken);
        return new AiPipelineRunResponse(
            Success: true,
            Message: $"AI分析が正常に完了しました。{created.Count}件の提案を登録しました。",
            CreatedCount: created.Count,
            Steps: steps
        );
    }

    public IAsyncEnumerable<AiPipelineStreamEvent> StreamManualPipelineAsync(CancellationToken cancellationToken = default)
    {
        return _orchestratorAgent.RunFullPipelineStreamAsync(cancellationToken);
    }
}

