using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly PosPromotionEngine _posPromotionEngine;
    private readonly InventoryService _inventoryService;
    private readonly AppDbContext _context;

    public PosController(
        PosPromotionEngine posPromotionEngine,
        InventoryService inventoryService,
        AppDbContext context)
    {
        _posPromotionEngine = posPromotionEngine;
        _inventoryService = inventoryService;
        _context = context;
    }

    [HttpPost("recommendations")]
    public async Task<ActionResult<PosRecommendationResponse>> GetRecommendations([FromBody] PosRecommendationRequest request)
    {
        // High speed recommendation under 500ms without LLM latency
        var response = await _posPromotionEngine.GetRecommendationsForCartAsync(request);
        return Ok(response);
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout([FromBody] CheckoutRequest request)
    {
        var store = await _context.Stores.FirstOrDefaultAsync();
        int storeId = store?.Id ?? 1;

        var staff = await _context.Users.FirstOrDefaultAsync(u => u.Role == "STAFF");
        int? staffId = staff?.Id;

        var response = await _inventoryService.ProcessCheckoutAsync(staffId, storeId, request);
        return Ok(response);
    }

    [HttpGet("customers")]
    public async Task<ActionResult<List<Customer>>> GetCustomers()
    {
        var customers = await _context.Customers.OrderBy(c => c.Name).ToListAsync();
        return Ok(customers);
    }
}
