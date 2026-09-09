using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IProductService
{
    Task<List<PosProductDto>> GetProductsAsync(string? searchQuery, CancellationToken cancellationToken = default);
    Task<List<ProductCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}

