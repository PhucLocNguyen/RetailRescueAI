using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryBatchDto>> GetBatchesAsync(CancellationToken cancellationToken = default);
    Task<List<ExpiryRiskDto>> GetExpiryRiskAnalysisAsync(CancellationToken cancellationToken = default);
    Task<InventoryBatch?> DeductBatchInventoryFefoAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    Task ResetDemoDataAsync(CancellationToken cancellationToken = default);
}
