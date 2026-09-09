using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var passwordHash = DbInitializer.HashPassword(request.Password);
        var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);

        if (user == null || user.PasswordHash != passwordHash)
        {
            return new LoginResponse(false, "ユーザー名またはパスワードが無効です。", string.Empty, null);
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

        return new LoginResponse(true, "ログインに成功しました。", token, userDto);
    }

    private string GenerateJwtToken(User user)
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

