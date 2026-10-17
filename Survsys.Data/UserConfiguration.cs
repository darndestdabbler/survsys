using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="User"/> onto the <c>users</c> table, one row per user name.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(user => user.Active).HasColumnName("active");

        builder.HasIndex(user => user.UserName).IsUnique();
    }
}
