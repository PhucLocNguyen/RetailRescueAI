using Microsoft.AspNetCore.SignalR;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Hubs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class PromotionService : IPromotionService
{
    private readonly IPromotionRepository _promotionRepository;
    private readonly IHubContext<PromotionHub> _hubContext;

    public PromotionService(
        IPromotionRepository promotionRepository,
        IHubContext<PromotionHub> hubContext)
    {
        _promotionRepository = promotionRepository;
        _hubContext = hubContext;
    }

    public async Task<List<PromotionDto>> GetPromotionsAsync(string? status, CancellationToken cancellationToken = default)
    {
        var promotions = await _promotionRepository.GetPromotionsAsync(status, cancellationToken);

        return promotions.Select(p => new PromotionDto(
            p.Id,
            p.PromotionCode,
            p.Name,
            p.PromotionType,
            p.Status,
            p.TargetProductId,
            p.TargetProduct?.Name,
            p.TargetBatch?.BatchCode,
            p.DiscountPercent,
            p.ComboPrice,
            p.StartTime,
            p.EndTime,
            p.CreatedVia,
            p.CreatedBy,
            p.ApprovedBy,
            p.ApprovedAt,
            p.AiReasoning,
            p.CreatedAt,
            p.ComboProductId,
            p.ComboProduct?.Name,
            p.ComboDiscountAmount
        )).ToList();
    }

    public async Task<PromotionDto> CreatePromotionAsync(CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var now = RetailRescueAI.Backend.Common.AppClock.Now;
        var promoCode = $"PROMO-{now:yyyyMMddHHmmss}";

        var promo = new Promotion
        {
            PromotionCode = promoCode,
            Name = request.Name,
            PromotionType = request.PromotionType,
            Status = "PENDING", // Always PENDING initially (BR-001)
            TargetProductId = request.TargetProductId,
            TargetBatchId = request.TargetBatchId,
            DiscountPercent = request.DiscountPercent,
            ComboPrice = request.ComboPrice,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CreatedVia = "MANAGER",
            CreatedBy = "佐藤 店長",
            AiReasoning = request.Reasoning,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _promotionRepository.AddAsync(promo, cancellationToken);
        await _promotionRepository.SaveChangesAsync(cancellationToken);

        var created = await _promotionRepository.GetPromotionWithDetailsAsync(promo.Id, cancellationToken);
        var target = created ?? promo;

        return new PromotionDto(
            target.Id,
            target.PromotionCode,
            target.Name,
            target.PromotionType,
            target.Status,
            target.TargetProductId,
            target.TargetProduct?.Name,
            target.TargetBatch?.BatchCode,
            target.DiscountPercent,
            target.ComboPrice,
            target.StartTime,
            target.EndTime,
            target.CreatedVia,
            target.CreatedBy,
            target.ApprovedBy,
            target.ApprovedAt,
            target.AiReasoning,
            target.CreatedAt,
            target.ComboProductId,
            target.ComboProduct?.Name,
            target.ComboDiscountAmount
        );
    }

    public async Task<bool> ApprovePromotionAsync(int id, CancellationToken cancellationToken = default)
    {
        var promo = await _promotionRepository.GetPromotionWithDetailsAsync(id, cancellationToken);
        if (promo == null) return false;

        promo.Status = "APPROVED";
        promo.ApprovedBy = "佐藤 店長 (Manager)";
        promo.ApprovedAt = RetailRescueAI.Backend.Common.AppClock.Now;
        promo.UpdatedAt = RetailRescueAI.Backend.Common.AppClock.Now;

        _promotionRepository.Update(promo);
        await _promotionRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await _hubContext.Clients.All.SendAsync("PromotionApproved", new
            {
                promotionId = promo.Id,
                promotionCode = promo.PromotionCode,
                promotionName = promo.Name,
                targetProductId = promo.TargetProductId,
                targetProductName = promo.TargetProduct?.Name,
                targetBatchId = promo.TargetBatchId,
                targetBatchCode = promo.TargetBatch?.BatchCode,
                discountPercent = promo.DiscountPercent,
                startTime = promo.StartTime,
                endTime = promo.EndTime,
                message = $"【値引き承認】「{promo.Name}」が店長により承認されました！"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SignalR] Broadcast error: {ex.Message}");
        }

        return true;
    }

    public async Task<bool> RejectPromotionAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var promo = await _promotionRepository.GetByIdAsync(id, cancellationToken);
        if (promo == null) return false;

        promo.Status = "REJECTED";
        promo.RejectionReason = reason;
        promo.UpdatedAt = RetailRescueAI.Backend.Common.AppClock.Now;

        _promotionRepository.Update(promo);
        await _promotionRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}

