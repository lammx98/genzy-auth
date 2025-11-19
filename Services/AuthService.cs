using System.Text.Json;
using Genzy.Auth.Data;
using Genzy.Auth.DTO;
using Genzy.Auth.Models;
using Genzy.Base.Utils;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;

namespace Genzy.Auth.Services;

public class AuthService(
    TokenService tokenService,
    AccountService accountService,
    AppDbContext context,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    SnowflakeIdGenerator idGen)
{
    private readonly AppDbContext _context = context;
    private readonly AccountService _accountService = accountService;
    private readonly TokenService _tokenService = tokenService;
    private readonly IConfiguration _configuration = configuration;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly SnowflakeIdGenerator _idGen = idGen;

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _accountService.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new Exception("Invalid email or password");
        }

        if (!_accountService.VerifyPassword(user, request.Password))
        {
            throw new Exception("Invalid email or password");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var user = new Account
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            Provider = "Local"
        };

        var result = await _accountService.CreateAsync(user, request.Password);
        if (result.Id == null)
        {
            throw new Exception("Create account failed");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.Token == token);

        if (refreshToken == null || refreshToken.Account == null)
        {
            throw new Exception("Invalid refresh token");
        }

        if (refreshToken.ExpiryDate < DateTime.UtcNow || refreshToken.IsRevoked)
        {
            throw new Exception("Refresh token expired or revoked");
        }

        // Revoke the old refresh token (rotation)
        refreshToken.IsRevoked = true;
        await _context.SaveChangesAsync();

        // Generate new tokens
        return await GenerateAuthResponseAsync(refreshToken.Account);
    }

    public async Task RevokeTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
        if (refreshToken != null)
        {
            refreshToken.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<AuthResponse> GenerateAuthResponseAsync(Account user)
    {
        var jwtToken = _tokenService.GenerateJwtToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = _idGen.NextId(),
            Token = refreshToken,
            AccountId = user.Id!,
            ExpiryDate = _tokenService.GetRefreshTokenExpiryTime()
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            Token = jwtToken,
            RefreshToken = refreshToken,
            Email = user.Email!,
            FullName = user.FullName ?? user.UserName!,
            PictureUrl = user.AvatarUrl
        };
    }

    public async Task<AuthResponse> HandleExternalLoginAsync(ExternalLoginRequest request)
    {
        var externalUser = await GetExternalUserInfoAsync(request);

        // Check if user already exists by external ID
        var user = await _accountService.FindByExternalIdAsync(externalUser.Provider!, externalUser.ExternalId!);
        
        // If not found by external ID, check by email
        if (user == null)
        {
            user = await _accountService.FindByEmailAsync(externalUser.Email!);
            
            // If user exists with same email but different provider, link the accounts
            if (user != null && string.IsNullOrEmpty(user.ExternalId))
            {
                user.Provider = externalUser.Provider;
                user.ExternalId = externalUser.ExternalId;
                user.AvatarUrl ??= externalUser.AvatarUrl;
                _context.Accounts.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        // Create new account if not found
        if (user == null)
        {
            user = new Account
            {
                UserName = externalUser.Email,
                Email = externalUser.Email,
                FullName = externalUser.FullName,
                AvatarUrl = externalUser.AvatarUrl,
                Provider = externalUser.Provider,
                ExternalId = externalUser.ExternalId
            };

            var result = await _accountService.CreateAsync(user);
            if (result.Id == null)
            {
                throw new Exception("Failed to create account");
            }
        }

        return await GenerateAuthResponseAsync(user);
    }

    private async Task<Account> GetExternalUserInfoAsync(ExternalLoginRequest request)
    {
        return request.Provider.ToLower() switch
        {
            "google" => await GetGoogleUserInfoAsync(request.Token),
            "facebook" => await GetFacebookUserInfoAsync(request.Token),
            _ => throw new Exception($"Provider '{request.Provider}' is not supported")
        };
    }

    private async Task<Account> GetGoogleUserInfoAsync(string idToken)
    {
        try
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId! }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new Account
            {
                Email = payload.Email,
                UserName = payload.Email,
                FullName = payload.Name,
                AvatarUrl = payload.Picture,
                Provider = "Google",
                ExternalId = payload.Subject
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to validate Google token: {ex.Message}");
        }
    }

    private async Task<Account> GetFacebookUserInfoAsync(string accessToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(
                $"https://graph.facebook.com/me?fields=id,name,email,picture&access_token={accessToken}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to validate Facebook token");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            var id = data.GetProperty("id").GetString();
            var name = data.GetProperty("name").GetString();
            var email = data.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
            var picture = data.TryGetProperty("picture", out var picProp) 
                ? picProp.GetProperty("data").GetProperty("url").GetString() 
                : null;

            if (string.IsNullOrEmpty(email))
            {
                throw new Exception("Facebook account must have a verified email");
            }

            return new Account
            {
                Email = email,
                UserName = email,
                FullName = name,
                AvatarUrl = picture,
                Provider = "Facebook",
                ExternalId = id
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to validate Facebook token: {ex.Message}");
        }
    }
}