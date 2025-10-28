using genzy_auth.Data;
using genzy_auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace genzy_auth.Services;

public class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokenService;
    private readonly ApplicationDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokenService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _context = context;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            throw new Exception("Invalid password");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            Provider = "Local"
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token);

        if (refreshToken == null || refreshToken.User == null)
        {
            throw new Exception("Invalid refresh token");
        }

        if (refreshToken.ExpiryDate < DateTime.UtcNow || refreshToken.IsRevoked)
        {
            throw new Exception("Refresh token expired");
        }

        return await GenerateAuthResponseAsync(refreshToken.User);
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

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var jwtToken = _tokenService.GenerateJwtToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
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
            PictureUrl = user.PictureUrl
        };
    }

    public async Task<AuthResponse> HandleExternalLoginAsync(ExternalLoginRequest request)
    {
        // Note: In a real application, you would verify the token with the provider
        // and get the user information from them. This is a simplified version.
        var externalUser = await GetExternalUserInfoAsync(request);
        
        var user = await _userManager.FindByEmailAsync(externalUser.Email!);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = externalUser.Email,
                Email = externalUser.Email,
                FullName = externalUser.FullName,
                PictureUrl = externalUser.PictureUrl,
                Provider = request.Provider,
                ExternalId = externalUser.Id
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        return await GenerateAuthResponseAsync(user);
    }

    private async Task<ApplicationUser> GetExternalUserInfoAsync(ExternalLoginRequest request)
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

    private async Task<ApplicationUser> GetGoogleUserInfoAsync(string token)
    {
        // Make API call to Google
        throw new NotImplementedException();
    }

    private async Task<ApplicationUser> GetFacebookUserInfoAsync(string token)
    {
        // Make API call to Facebook
        throw new NotImplementedException();
    }

    private async Task<ApplicationUser> GetTikTokUserInfoAsync(string token)
    {
        // Make API call to TikTok
        throw new NotImplementedException();
    }
}