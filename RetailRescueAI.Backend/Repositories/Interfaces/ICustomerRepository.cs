using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken = default);
    Task RecordPurchaseHistoryAsync(int customerId, int productId, int quantity, decimal unitPrice, DateTime date, CancellationToken cancellationToken = default);
    Task AddLoyaltyPointsAsync(int customerId, int points, CancellationToken cancellationToken = default);
}

