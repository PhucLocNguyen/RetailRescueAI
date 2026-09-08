using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PosProductDto>>> GetAll([FromQuery] string? q)
    {
        var now = DateTime.UtcNow;

        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.ProductCode.ToLower().Contains(search) ||
                p.Barcode.Contains(search));
        }

        var products = await query.ToListAsync();

        var dtos = products.Select(p =>
        {
            var activeBatches = p.Batches.Where(b => b.RemainingQuantity > 0).OrderBy(b => b.ExpiryDate).ToList();
            var totalStock = activeBatches.Sum(b => b.RemainingQuantity);
            var earliestExpiry = activeBatches.FirstOrDefault()?.ExpiryDate;

            string expiryFormatted = earliestExpiry.HasValue
                ? $"{earliestExpiry.Value:yyyy/MM/dd HH:mm} (残り{(earliestExpiry.Value - now).TotalHours:F0}時間)"
                : "在庫なし";

            return new PosProductDto(
                p.Id,
                p.ProductCode,
                p.Name,
                p.Description,
                p.Category?.Name ?? "その他",
                p.Price,
                p.Barcode,
                p.ImageUrl,
                totalStock,
                expiryFormatted
            );
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("categories")]
    public async Task<ActionResult> GetCategories()
    {
        var categories = await _context.ProductCategories.ToListAsync();
        return Ok(categories);
    }
}

