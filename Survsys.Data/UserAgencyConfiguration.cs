using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="UserAgency"/> onto the <c>user_agencies</c> table, keyed by the
/// pair, so a user works for an agency once. The rows go with the user or the
/// agency.
/// </summary>
internal sealed class UserAgencyConfiguration : IEntityTypeConfiguration<UserAgency>
{
    public void Configure(EntityTypeBuilder<UserAgency> builder)
    {
        builder.ToTable("user_agencies");
        builder.HasKey(userAgency => new { userAgency.UserId, userAgency.AgencyId });

        builder.Property(userAgency => userAgency.UserId).HasColumnName("user_id");
        builder.Property(userAgency => userAgency.AgencyId).HasColumnName("agency_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(userAgency => userAgency.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(userAgency => userAgency.AgencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
