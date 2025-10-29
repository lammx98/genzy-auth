using Genzy.Base.Models;

namespace Genzy.Auth.Models;

public class RefreshToken : BaseModel
{
    public required string Token { get; set; }
    public required string AccountId { get; set; }
    public required DateTime ExpiryDate { get; set; }
    public bool IsRevoked { get; set; }
    public Account? Account { get; set; }
}