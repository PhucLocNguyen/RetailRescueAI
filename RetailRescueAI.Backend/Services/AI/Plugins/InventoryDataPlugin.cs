using System.ComponentModel;
using Microsoft.SemanticKernel;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Services.AI.Plugins;

public class InventoryDataPlugin
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly ILogger<InventoryDataPlugin> _logger;

    public InventoryDataPlugin(IInventoryBatchRepository batchRepository, ILogger<InventoryDataPlugin> logger)
    {
        _batchRepository = batchRepository;
        _logger = logger;
    }

    [KernelFunction, Description("Gets all active inventory batches with non-zero stock for store analysis")]
    public async Task<List<InventoryBatch>> GetActiveInventoryBatchesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[InventoryDataPlugin] Querying active inventory batches from database...");
        return await _batchRepository.GetActiveBatchesAsync(cancellationToken);
    }

    [KernelFunction, Description("Gets batch details by batch code (e.g. BATCH-SAND-001)")]
    public async Task<InventoryBatch?> GetBatchByCodeAsync(
        [Description("Unique batch code string")] string batchCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[InventoryDataPlugin] Finding batch by code: {BatchCode}", batchCode);
        return await _batchRepository.GetByBatchCodeAsync(batchCode, cancellationToken);
    }

    [KernelFunction, Description("Updates the risk status of an inventory batch (e.g., CRITICAL, AT_RISK, EXPIRED)")]
    public async Task<bool> UpdateBatchStatusAsync(
        [Description("Database ID of the batch")] int batchId,
        [Description("New risk status: CRITICAL, AT_RISK, MEDIUM, LOW, EXPIRED")] string newStatus,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        if (batch == null) return false;

        batch.Status = newStatus.ToUpperInvariant();
        batch.UpdatedAt = DateTime.UtcNow.AddHours(7);
        batch.UpdatedAt = RetailRescueAI.Backend.Common.AppClock.Now;
        _batchRepository.Update(batch);
        await _batchRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[InventoryDataPlugin] Updated batch {BatchCode} status to {Status}", batch.BatchCode, newStatus);
        return true;
    }
}

