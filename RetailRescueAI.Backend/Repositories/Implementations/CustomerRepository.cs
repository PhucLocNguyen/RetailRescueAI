using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task RecordPurchaseHistoryAsync(int customerId, int productId, int quantity, decimal unitPrice, DateTime date, CancellationToken cancellationToken = default)
    {
        await _context.CustomerPurchaseHistories.AddAsync(new CustomerPurchaseHistory
        {
            CustomerId = customerId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            PurchaseDate = date
        }, cancellationToken);
    }

    public async Task AddLoyaltyPointsAsync(int customerId, int points, CancellationToken cancellationToken = default)
    {
        var customer = await _dbSet.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer != null)
        {
            customer.Points += points;
        }
    }
}

