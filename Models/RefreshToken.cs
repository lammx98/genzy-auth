namespace genzy_auth.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public required string Token { get; set; }
    public required string UserId { get; set; }
    public required DateTime ExpiryDate { get; set; }
    public bool IsRevoked { get; set; }
    public ApplicationUser? User { get; set; }
}