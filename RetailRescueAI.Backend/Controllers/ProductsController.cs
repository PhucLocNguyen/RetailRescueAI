using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PosProductDto>>> GetAll([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var dtos = await _productService.GetProductsAsync(q, cancellationToken);
        return Ok(dtos);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<ProductCategory>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _productService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }
}

