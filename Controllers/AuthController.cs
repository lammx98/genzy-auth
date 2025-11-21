using System.Security.Claims;
using Genzy.Auth.DTO;
using Genzy.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Genzy.Auth.Models;
using Genzy.Base.Extensions;

namespace Genzy.Auth.Controllers;

[ApiController]
[Route("")]
public class AuthController(AuthService authService, AccountService accountService, TokenService tokenService) : ControllerBase
{
    private readonly AuthService _authService = authService;
    private readonly AccountService _accountService = accountService;
    private readonly TokenService _tokenService = tokenService;

    [HttpGet("google-login")]
    public IActionResult GoogleLogin(string? returnUrl = "/")
    {
        // After Google signs in via its internal callback, redirect back to our API callback
        var redirectUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme) ?? "/auth/google-callback";
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = "/")
    {
        // Try to get id_token saved by Google handler
        var idToken = await HttpContext.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, "id_token");
        AuthResponse? auth;
        if (!string.IsNullOrEmpty(idToken))
        {
            // Use existing external-login flow to create/find user and issue tokens
            auth = await _authService.HandleExternalLoginAsync(new DTO.ExternalLoginRequest
            {
                Provider = "google",
                Token = idToken
            });
        }
        else
        {
            // Fallback: build user from claims and issue tokens
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = result.Principal;
            if (principal == null)
            {
                return BadRequest("Google authentication failed");
            }

            var email = principal.FindFirstValue(ClaimTypes.Email);
            var name = principal.FindFirstValue(ClaimTypes.Name);
            var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Google account has no email");
            }

            var user = await _accountService.FindByEmailAsync(email);
            if (user == null)
            {
                user = new Account
                {
                    UserName = email,
                    Email = email,
                    FullName = name,
                    Provider = "Google",
                    ExternalId = externalId,
                    AvatarUrl = principal.FindFirstValue("picture")
                };
                await _accountService.CreateAsync(user);
            }

            // Generate tokens and persist refresh token to database
            auth = await _authService.GenerateAuthResponseAsync(user);
        }

        // Redirect to frontend with tokens
        System.Console.WriteLine(auth.Token);
        var frontendUrl = $"http://localhost:3020/auth/callback?token={Uri.EscapeDataString(auth.Token)}&refreshToken={Uri.EscapeDataString(auth.RefreshToken)}";
        return Redirect(frontendUrl);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            Name = User.Identity?.Name,
            Email = User.FindFirstValue(ClaimTypes.Email)
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] string refreshToken)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(refreshToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken([FromBody] string refreshToken)
    {
        try
        {
            await _authService.RevokeTokenAsync(refreshToken);
            return Ok(new { message = "Token revoked successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] string refreshToken)
    {
        try
        {
            await _authService.RevokeTokenAsync(refreshToken);
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("external-login")]
    public async Task<ActionResult<AuthResponse>> ExternalLogin(ExternalLoginRequest request)
    {
        try
        {
            var response = await _authService.HandleExternalLoginAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("test-token")]
    public IActionResult TestToken([FromBody] string token)
    {
        try
        {
            // Log token info
            Console.WriteLine($"Received token length: {token?.Length ?? 0}");
            Console.WriteLine($"Token starts with: {token?.Substring(0, Math.Min(50, token?.Length ?? 0))}");
            Console.WriteLine($"Token ends with: {token?.Substring(Math.Max(0, (token?.Length ?? 0) - 20))}");

            // Check if it's a valid JWT format (3 parts separated by dots)
            var parts = token?.Split('.');
            Console.WriteLine($"Token parts count: {parts?.Length ?? 0}");

            if (parts?.Length == 3)
            {
                Console.WriteLine($"Header part length: {parts[0].Length}");
                Console.WriteLine($"Payload part length: {parts[1].Length}");
                Console.WriteLine($"Signature part length: {parts[2].Length}");

                // Try to decode header
                try
                {
                    var headerBytes = Convert.FromBase64String(parts[0].PadBase64());
                    var headerJson = System.Text.Encoding.UTF8.GetString(headerBytes);
                    Console.WriteLine($"Header decoded: {headerJson}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to decode header: {ex.Message}");
                }
            }

            // Try to validate with TokenService
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            return Ok(new
            {
                Valid = true,
                TokenId = jwtToken.Id,
                Issuer = jwtToken.Issuer,
                Audience = jwtToken.Audiences.FirstOrDefault(),
                Claims = jwtToken.Claims.Select(c => new { c.Type, c.Value })
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token validation error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}