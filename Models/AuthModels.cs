namespace genzy_auth.Models;

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FullName { get; set; }
}

public class ExternalLoginRequest
{
    public required string Provider { get; set; }
    public required string Token { get; set; }
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public string? PictureUrl { get; set; }
}