using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IPosService _posService;

    public PosController(IPosService posService)
    {
        _posService = posService;
    }

    [HttpPost("recommendations")]
    public async Task<ActionResult<PosRecommendationResponse>> GetRecommendations(
        [FromBody] PosRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _posService.GetRecommendationsForCartAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _posService.CheckoutAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("customers")]
    public async Task<ActionResult<List<Customer>>> GetCustomers(CancellationToken cancellationToken)
    {
        var customers = await _posService.GetCustomersAsync(cancellationToken);
        return Ok(customers);
    }
}
