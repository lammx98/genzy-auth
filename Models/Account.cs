using Genzy.Base.Models;

namespace Genzy.Auth.Models;

public class Account
{
    public string? Id { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Provider { get; set; }
    public string? ExternalId { get; set; }
}