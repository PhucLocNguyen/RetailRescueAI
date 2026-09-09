using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        if (!response.Success)
        {
            return Unauthorized(response);
        }

        return Ok(response);
    }
}

