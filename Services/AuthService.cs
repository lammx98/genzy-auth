using Genzy.Auth.Data;
using Genzy.Auth.DTO;
using Genzy.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Genzy.Auth.Services;

public class AuthService(
    TokenService tokenService,
AccountService accountService,
    AppDbContext context)
{
    private readonly AppDbContext _context = context;
    private readonly AccountService _accountService = accountService;
    private readonly TokenService _tokenService = tokenService;

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _accountService.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        // var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        // if (!result.Succeeded)
        // {
        //     throw new Exception("Invalid password");
        // }

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
            throw new Exception("Refresh token expired");
        }

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

    private async Task<AuthResponse> GenerateAuthResponseAsync(Account user)
    {
        var jwtToken = _tokenService.GenerateJwtToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
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
        // Note: In a real application, you would verify the token with the provider
        // and get the user information from them. This is a simplified version.
        var externalUser = await GetExternalUserInfoAsync(request);

        var user = await _accountService.FindByEmailAsync(externalUser.Email!);
        if (user == null)
        {
            user = new Account
            {
                UserName = externalUser.Email,
                Email = externalUser.Email,
                FullName = externalUser.FullName,
                AvatarUrl = externalUser.AvatarUrl,
                Provider = request.Provider,
                ExternalId = externalUser.Id
            };

            var result = await _accountService.CreateAsync(user);
            if (result.Id == null)
            {
                throw new Exception("Create account failed");
            }
        }

        return await GenerateAuthResponseAsync(user);
    }

    private async Task<Account> GetExternalUserInfoAsync(ExternalLoginRequest request)
    {
        // This is where you would make API calls to the respective providers
        // to verify the token and get user information
        // This is a simplified version
        return request.Provider switch
        {
            "Google" => await GetGoogleUserInfoAsync(request.Token),
            "Facebook" => await GetFacebookUserInfoAsync(request.Token),
            "TikTok" => await GetTikTokUserInfoAsync(request.Token),
            _ => throw new Exception("Invalid provider")
        };
    }

    private async Task<Account> GetGoogleUserInfoAsync(string token)
    {
        // Make API call to Google
        throw new NotImplementedException();
    }

    private async Task<Account> GetFacebookUserInfoAsync(string token)
    {
        // Make API call to Facebook
        throw new NotImplementedException();
    }

    private async Task<Account> GetTikTokUserInfoAsync(string token)
    {
        // Make API call to TikTok
        throw new NotImplementedException();
    }
}