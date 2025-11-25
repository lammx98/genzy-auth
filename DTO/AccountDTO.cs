namespace Genzy.Auth.DTO;

public class AccountDTO
{
    public long? Id { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
}
