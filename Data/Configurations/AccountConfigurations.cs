using Genzy.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Genzy.Auth.Data.Configurations;

public class AccountConfigurations : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").HasMaxLength(50).IsRequired();
        builder.Property(o => o.UserName).HasColumnName("username").HasMaxLength(255);
        builder.Property(o => o.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(o => o.PasswordHash).HasColumnName("password_hash").HasMaxLength(2000);
        builder.Property(o => o.FullName).HasColumnName("fullname").HasMaxLength(255);
        builder.Property(o => o.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
        builder.Property(o => o.Provider).HasColumnName("provider").HasMaxLength(20);
        builder.Property(o => o.ExternalId).HasColumnName("external_id").HasMaxLength(50);
    }
}
