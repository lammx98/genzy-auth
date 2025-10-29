namespace Genzy.Auth.DTO;

public class AuthResponse
{
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public string? PictureUrl { get; set; }
}