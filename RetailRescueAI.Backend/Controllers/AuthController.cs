using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var passwordHash = DbInitializer.HashPassword(request.Password);

        var user = await _context.Users
            .Include(u => u.Store)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

        if (user == null || user.PasswordHash != passwordHash)
        {
            return Unauthorized(new LoginResponse(false, "ユーザー名またはパスワードが無効です。", string.Empty, null));
        }

        var token = GenerateJwtToken(user);

        var userDto = new UserDto(
            user.Id,
            user.Username,
            user.FullName,
            user.Role,
            user.StoreId,
            user.Store?.Name
        );

        return Ok(new LoginResponse(true, "ログインに成功しました。", token, userDto));
    }

    private string GenerateJwtToken(Models.User user)
    {
        var secretKey = _configuration["Jwt:Key"] ?? "RetailRescueAI_SuperSecretKey_2026_Enterprise_Security_JwtToken!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("FullName", user.FullName),
            new Claim("StoreId", user.StoreId?.ToString() ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "RetailRescueAI",
            audience: _configuration["Jwt:Audience"] ?? "RetailRescueAIApp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

