using RetailRescueAI.Backend.DTOs;
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

    public AiRecommendationService(
        IAiRecommendationRepository recommendationRepository,
        IPromotionRepository promotionRepository,
        OrchestratorAgent orchestratorAgent)
    {
        _recommendationRepository = recommendationRepository;
        _promotionRepository = promotionRepository;
        _orchestratorAgent = orchestratorAgent;
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
            r.Evidences.Select(e => new AIEvidenceDto(e.EvidenceKey, e.EvidenceValue, e.Description)).ToList()
        )).ToList();
    }

    public async Task<(bool Success, string Message, int? PromotionId)> ApproveRecommendationAsync(
        int id,
        ApproveRecommendationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var rec = await _recommendationRepository.GetWithDetailsAsync(id, cancellationToken);
        if (rec == null) return (false, "AI提案が見つかりません。", null);

        var now = DateTime.UtcNow;
        rec.Status = "APPROVED";
        rec.ReviewedAt = now;
        rec.ReviewedBy = "佐藤 店長 (Manager)";

        var promo = new Promotion
        {
            PromotionCode = $"PROMO-{now:yyyyMMddHHmmss}",
            Name = rec.RecommendedAction,
            PromotionType = rec.RecommendationType,
            Status = "APPROVED",
            TargetProductId = rec.TargetProductId,
            TargetBatchId = rec.TargetBatchId,
            DiscountPercent = rec.RecommendedDiscountPercent,
            ComboPrice = rec.RecommendedComboPrice,
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

        return (true, $"提案「{rec.RecommendedAction}」を承認しました。プロモーションがレジで有効化されました。", promo.Id);
    }

    public async Task<bool> RejectRecommendationAsync(int id, RejectRecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var rec = await _recommendationRepository.GetByIdAsync(id, cancellationToken);
        if (rec == null) return false;

        rec.Status = "REJECTED";
        rec.ReviewedAt = DateTime.UtcNow;
        rec.ReviewedBy = "佐藤 店長 (Manager)";

        _recommendationRepository.Update(rec);
        await _recommendationRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RunManualPipelineAsync(CancellationToken cancellationToken = default)
    {
        var created = await _orchestratorAgent.RunFullPipelineAsync(cancellationToken);
        return created.Count;
    }
}

