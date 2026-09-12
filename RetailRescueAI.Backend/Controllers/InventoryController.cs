using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("batches")]
    public async Task<ActionResult<List<InventoryBatchDto>>> GetBatches(CancellationToken cancellationToken)
    {
        var dtos = await _inventoryService.GetBatchesAsync(cancellationToken);
        return Ok(dtos);
    }

    [HttpGet("expiry-risk")]
    public async Task<ActionResult<List<ExpiryRiskDto>>> GetExpiryRiskAnalysis(CancellationToken cancellationToken)
    {
        var dtos = await _inventoryService.GetExpiryRiskAnalysisAsync(cancellationToken);
        return Ok(dtos);
    }

    [HttpPost("reset-demo")]
    public async Task<ActionResult> ResetDemoData(CancellationToken cancellationToken)
    {
        await _inventoryService.ResetDemoDataAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            message = "デモデータを正常にリセットしました。全ロットの賞味期限が現在時刻を基準に再同期されました。"
        });
    }
}
