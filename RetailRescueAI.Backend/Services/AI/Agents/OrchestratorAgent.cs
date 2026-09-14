using RetailRescueAI.Backend.DTOs;
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
        var (recommendations, _) = await RunFullPipelineWithTraceAsync(cancellationToken);
        return recommendations;
    }

    public async Task<(List<AIRecommendation> Recommendations, List<AiAgentTraceStepDto> Steps)> RunFullPipelineWithTraceAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation("[OrchestratorAgent] Starting AI Retail Rescue Analysis Pipeline...");
        _logger.LogInformation("=================================================");

        var now = RetailRescueAI.Backend.Common.AppClock.Now;
        var steps = new List<AiAgentTraceStepDto>();

        // 1. Fetch active inventory batches via Repository
        var batches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);

        if (batches.Count == 0)
        {
            _logger.LogInformation("[OrchestratorAgent] No active batches found.");
            return (new List<AIRecommendation>(), steps);
        }

        // 2. Expiry Agent (Semantic Kernel)
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var expiryResults = await _expiryAgent.AnalyzeBatchesAsync(batches, now, cancellationToken);

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
        sw1.Stop();

        var expiryDetails = new List<string>
        {
            $"全店舗スキャン対象: {batches.Count} 件の有効ロット在庫 (InventoryDataPlugin)"
        };
        foreach (var exp in expiryResults)
        {
            string icon = exp.RiskLevel switch { "CRITICAL" => "🔴", "AT_RISK" => "🟠", _ => "🟢" };
            expiryDetails.Add($"{icon} {exp.Batch.BatchCode} ({exp.Batch.Product?.Name}): 残り{exp.HoursUntilExpiry:F1}時間 (リスク: {exp.RiskLevel})");
        }

        steps.Add(new AiAgentTraceStepDto(
            AgentKey: "ExpiryAgent",
            AgentName: "賞味期限リスク監視エージェント (Semantic Kernel)",
            RoleTitle: "Expiry Risk Monitoring Agent (SK Plugin)",
            Description: "全ロットの賞味期限と残存時間をリアルタイムにスキャンし、即時対応が必要な在庫を特定",
            Details: expiryDetails,
            Status: "COMPLETED",
            DurationMs: (int)sw1.ElapsedMilliseconds
        ));

        // 3. Sales Analysis Agent (Semantic Kernel)
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var salesResults = await _salesAgent.AnalyzeSalesVelocityAsync(expiryResults, now, cancellationToken);
        sw2.Stop();

        var salesDetails = new List<string>();
        foreach (var s in salesResults)
        {
            salesDetails.Add($"📊 {s.Batch.Product?.Name} ({s.Batch.BatchCode}): 日販 {s.AverageDailySales:F1}個/日 → 期限前消化予測 {s.EstimatedNormalSalesUntilExpiry}個 | 潜在廃棄 {s.PotentialWasteUnits}個 (損失見込 ¥{s.PotentialWasteFinancialLoss:N0})");
        }

        steps.Add(new AiAgentTraceStepDto(
            AgentKey: "SalesAgent",
            AgentName: "販売速度・廃棄予測エージェント (Semantic Kernel)",
            RoleTitle: "Sales Velocity & Waste Forecaster (SK Plugin)",
            Description: "過去7日間のPOS販売実績から日販ペースを算出し、期限切れまでの自然消化予測と潜在的損失を試算",
            Details: salesDetails,
            Status: "COMPLETED",
            DurationMs: (int)sw2.ElapsedMilliseconds
        ));

        // 4. Promotion Agent (Semantic Kernel & Gemini)
        var sw3 = System.Diagnostics.Stopwatch.StartNew();
        var proposals = await _promotionAgent.GenerateProposalsAsync(salesResults, now, cancellationToken);
        sw3.Stop();

        var promoDetails = new List<string>
        {
            "LLMエンジン: Google Gemini 2.5 Flash / Semantic Kernel ComboStrategyPlugin による最適施策立案"
        };
        foreach (var p in proposals)
        {
            promoDetails.Add($"💡 {p.ActionTitle}: 想定売上 {p.ExpectedSales}個 / 救済見込 +{p.ExpectedWasteReduction}個 / 回収収益 ¥{p.ExpectedRevenue:N0}");
        }

        steps.Add(new AiAgentTraceStepDto(
            AgentKey: "PromotionAgent",
            AgentName: "販促プロモーション立案エージェント (Semantic Kernel)",
            RoleTitle: "Smart Promotion Strategy Agent (SK & Gemini)",
            Description: "商品特性とピーク時間帯（夕方17:00〜22:00等）を踏まえ、最適な割引率と論理的な推奨理由を自動生成",
            Details: promoDetails,
            Status: "COMPLETED",
            DurationMs: (int)sw3.ElapsedMilliseconds
        ));

        // 5. Reviser Agent (Semantic Kernel Safety Guardrails)
        var sw4 = System.Diagnostics.Stopwatch.StartNew();
        var validatedResults = _reviserAgent.ValidateProposals(proposals);
        sw4.Stop();

        var reviserDetails = new List<string>();
        foreach (var vr in validatedResults)
        {
            if (vr.IsValid)
            {
                reviserDetails.Add($"🛡️ {vr.Proposal.ActionTitle}: ✅ 全ガードレール合格 (BR-003期限制約 / BR-006実在庫 / 最低粗利15%クリア)");
            }
            else
            {
                reviserDetails.Add($"⚠️ {vr.Proposal.ActionTitle}: ❌ 却下 ({string.Join(", ", vr.ValidationMessages)})");
            }
        }

        steps.Add(new AiAgentTraceStepDto(
            AgentKey: "ReviserAgent",
            AgentName: "安全制約検証エージェント (Semantic Kernel)",
            RoleTitle: "Retail Safety & Guardrails Agent (SK Plugin)",
            Description: "リテール規約 BR-003（賞味期限内）、BR-006（実在庫）、粗利率15%以上の厳格チェックを実施し提案を保護",
            Details: reviserDetails,
            Status: "COMPLETED",
            DurationMs: (int)sw4.ElapsedMilliseconds
        ));

        // 6. Save valid proposals as PENDING recommendations
        var sw5 = System.Diagnostics.Stopwatch.StartNew();
        var createdRecommendations = new List<AIRecommendation>();

        foreach (var vr in validatedResults)
        {
            if (!vr.IsValid)
            {
                _logger.LogWarning("[OrchestratorAgent] Proposal rejected by Reviser: {Title}", vr.Proposal.ActionTitle);
                continue;
            }

            var p = vr.Proposal;

            // Check if there is already a PENDING recommendation for this batch and promotion type
            var existingPending = await _recommendationRepository.GetPendingForBatchAsync(p.TargetBatch.Id, p.PromotionType, cancellationToken);

            if (existingPending != null)
            {
                _logger.LogInformation("[OrchestratorAgent] Recommendation ({Type}) already pending for batch {BatchCode}.", p.PromotionType, p.TargetBatch.BatchCode);
                continue;
            }

            var typeSuffix = p.PromotionType == "BUNDLE_COMBO" ? "COMBO" : "DISC";
            var recCode = $"REC-{RetailRescueAI.Backend.Common.AppClock.Now:yyyyMMddHHmmss}-{p.TargetBatch.Id}-{typeSuffix}";
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
                ComboProductId = p.ComboProduct?.Id,
                ComboProductName = p.ComboProduct?.Name,
                RecommendedComboSavings = p.ComboSavings,
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
        sw5.Stop();

        var orchDetails = new List<string>
        {
            $"🎯 Human-in-the-Loop 統合: {createdRecommendations.Count} 件の新規提案を「PENDING (店長承認待ち)」として登録完了",
            "店長ポータルで割引率の調整および承認が可能です。"
        };

        steps.Add(new AiAgentTraceStepDto(
            AgentKey: "OrchestratorAgent",
            AgentName: "統括オーケストレーター",
            RoleTitle: "Human-in-the-Loop Orchestrator",
            Description: "全エージェントの成果を総括し、勝手な自動値引きを防ぐため「店長承認待ち」としてキューに登録",
            Details: orchDetails,
            Status: "COMPLETED",
            DurationMs: (int)sw5.ElapsedMilliseconds
        ));

        _logger.LogInformation("[OrchestratorAgent] Pipeline finished. Created {Count} PENDING recommendations.", createdRecommendations.Count);
        return (createdRecommendations, steps);
    }
}

