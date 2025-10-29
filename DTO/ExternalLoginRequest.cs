namespace Genzy.Auth.DTO;

public class ExternalLoginRequest
{
    public required string Provider { get; set; }
    public required string Token { get; set; }
}