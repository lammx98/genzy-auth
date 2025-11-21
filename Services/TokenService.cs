using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Genzy.Auth.Models;
using Genzy.Base.Security.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Genzy.Auth.Services;

public class TokenService(JwtOptions jwtSettings)
{
    private readonly JwtOptions _jwtSettings = jwtSettings;

    public string GenerateJwtToken(Account user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        // Parse secret key consistently - try base64 first, fallback to UTF8
        byte[] key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.FullName ?? user.UserName!),
                new Claim("provider", user.Provider ?? "App")
            ]),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);
        
        // Validate token can be read back
        try
        {
            var validatedToken = tokenHandler.ReadJwtToken(tokenString);
            Console.WriteLine($"Generated valid JWT token for user {user.Email} (length: {tokenString.Length})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Generated token is invalid: {ex.Message}");
        }
        
        return tokenString;
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public DateTime GetRefreshTokenExpiryTime()
    {
        return DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);
    }
}