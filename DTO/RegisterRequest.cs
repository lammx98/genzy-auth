namespace Genzy.Auth.DTO;

public class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FullName { get; set; }
}