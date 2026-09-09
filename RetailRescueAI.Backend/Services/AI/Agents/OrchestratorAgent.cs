using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public class OrchestratorAgent
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly IAiRecommendationRepository _recommendationRepository;
    private readonly ExpiryAgent _expiryAgent;
    private readonly SalesAgent _salesAgent;
    private readonly PromotionAgent _promotionAgent;
    private readonly ReviserAgent _reviserAgent;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        IInventoryBatchRepository batchRepository,
        IAiRecommendationRepository recommendationRepository,
        ExpiryAgent expiryAgent,
        SalesAgent salesAgent,
        PromotionAgent promotionAgent,
        ReviserAgent reviserAgent,
        ILogger<OrchestratorAgent> logger)
    {
        _batchRepository = batchRepository;
        _recommendationRepository = recommendationRepository;
        _expiryAgent = expiryAgent;
        _salesAgent = salesAgent;
        _promotionAgent = promotionAgent;
        _reviserAgent = reviserAgent;
        _logger = logger;
    }

    public async Task<List<AIRecommendation>> RunFullPipelineAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation("[OrchestratorAgent] Starting AI Retail Rescue Analysis Pipeline...");
        _logger.LogInformation("=================================================");

        var now = DateTime.UtcNow;

        // 1. Fetch active inventory batches via Repository
        var batches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);

        if (batches.Count == 0)
        {
            _logger.LogInformation("[OrchestratorAgent] No active batches found.");
            return new List<AIRecommendation>();
        }

        // 2. Expiry Agent
        var expiryResults = _expiryAgent.AnalyzeBatches(batches, now);

        // Update batch statuses in DB
        foreach (var exp in expiryResults)
        {
            if (exp.Batch.Status != exp.RiskLevel)
            {
                exp.Batch.Status = exp.RiskLevel;
                exp.Batch.UpdatedAt = now;
                _batchRepository.Update(exp.Batch);
            }
        }
        await _batchRepository.SaveChangesAsync(cancellationToken);

        // 3. Sales Analysis Agent
        var salesResults = await _salesAgent.AnalyzeSalesVelocityAsync(expiryResults, now, cancellationToken);

        // 4. Promotion Agent
        var proposals = await _promotionAgent.GenerateProposalsAsync(salesResults, now, cancellationToken);

        // 5. Reviser Agent
        var validatedResults = _reviserAgent.ValidateProposals(proposals);

        // 6. Save valid proposals as PENDING recommendations
        var createdRecommendations = new List<AIRecommendation>();

        foreach (var vr in validatedResults)
        {
            if (!vr.IsValid)
            {
                _logger.LogWarning("[OrchestratorAgent] Proposal rejected by Reviser: {Title}", vr.Proposal.ActionTitle);
                continue;
            }

            var p = vr.Proposal;

            // Check if there is already a PENDING recommendation for this batch
            var existingPending = await _recommendationRepository.GetPendingForBatchAsync(p.TargetBatch.Id, cancellationToken);

            if (existingPending != null)
            {
                _logger.LogInformation("[OrchestratorAgent] Recommendation already pending for batch {BatchCode}.", p.TargetBatch.BatchCode);
                continue;
            }

            var recCode = $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{p.TargetBatch.Id}";
            var rec = new AIRecommendation
            {
                RecommendationCode = recCode,
                TargetProductId = p.TargetBatch.ProductId,
                TargetBatchId = p.TargetBatch.Id,
                RecommendationType = p.PromotionType,
                RiskLevel = p.RiskLevel,
                RecommendedAction = p.ActionTitle,
                RecommendedDiscountPercent = p.DiscountPercent,
                RecommendedComboPrice = p.ComboPrice,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                ExpectedSales = p.ExpectedSales,
                ExpectedWasteReduction = p.ExpectedWasteReduction,
                ExpectedRevenue = p.ExpectedRevenue,
                Reason = p.Reason,
                Status = "PENDING", // Human-in-the-Loop! Always PENDING
                CreatedAt = now
            };

            foreach (var kv in p.EvidenceMap)
            {
                rec.Evidences.Add(new AIRecommendationEvidence
                {
                    EvidenceKey = kv.Key,
                    EvidenceValue = kv.Value,
                    Description = $"自動算出根拠 ({kv.Key})"
                });
            }

            await _recommendationRepository.AddAsync(rec, cancellationToken);
            createdRecommendations.Add(rec);
        }

        await _recommendationRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[OrchestratorAgent] Pipeline finished. Created {Count} PENDING recommendations.", createdRecommendations.Count);
        return createdRecommendations;
    }
}

