using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface ISaleRepository : IRepository<Sale>
{
    Task<List<SaleItem>> GetRecentSaleItemsAsync(DateTime since, CancellationToken cancellationToken = default);
    Task<Sale> CreateSaleTransactionAsync(Sale sale, CancellationToken cancellationToken = default);
}

