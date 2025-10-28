using Microsoft.AspNetCore.Identity;

namespace genzy_auth.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? PictureUrl { get; set; }
    public string? Provider { get; set; } // "Local", "Google", "Facebook", "TikTok"
    public string? ExternalId { get; set; } // ID from external provider
}