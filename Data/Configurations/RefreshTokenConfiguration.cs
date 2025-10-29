using Genzy.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Genzy.Auth.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Token).HasColumnName("token").HasMaxLength(2000);
        builder.Property(o => o.AccountId).HasColumnName("user_id");
        builder.Property(o => o.ExpiryDate).HasColumnName("expiry_date");
        builder.Property(o => o.IsRevoked).HasColumnName("is_revoked");

        builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
    }
}
