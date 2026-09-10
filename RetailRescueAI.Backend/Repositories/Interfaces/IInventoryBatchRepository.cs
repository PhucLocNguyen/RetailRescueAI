using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface IInventoryBatchRepository : IRepository<InventoryBatch>
{
    Task<List<InventoryBatch>> GetAllBatchesWithProductAsync(CancellationToken cancellationToken = default);
    Task<List<InventoryBatch>> GetActiveBatchesAsync(CancellationToken cancellationToken = default);
    Task<List<InventoryBatch>> GetAvailableBatchesForProductFefoAsync(int productId, CancellationToken cancellationToken = default);
    Task<List<InventoryBatch>> GetUrgentBatchesAsync(CancellationToken cancellationToken = default);
    Task<InventoryBatch?> GetByBatchCodeAsync(string batchCode, CancellationToken cancellationToken = default);
}

