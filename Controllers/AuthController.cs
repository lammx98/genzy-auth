using System.Security.Claims;
using Genzy.Auth.DTO;
using Genzy.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Genzy.Auth.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    private readonly AuthService _authService = authService;

    [HttpGet("google-login")]
    public IActionResult GoogleLogin(string? returnUrl = "/")
    {
        var redirectUrl = "http://localhost:3000/auth/callback";
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = result.Principal?.Identities.FirstOrDefault()?.Claims;

        var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        // TODO: tạo JWT token cho frontend
        var jwt = "fake.jwt.token";

        return Redirect($"http://localhost:3000/auth/callback?token={jwt}");
    }

    [Authorize]
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
}