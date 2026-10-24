using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Agency"/> onto the <c>agencies</c> table, under the legacy
/// column names, seeded with the sixteen agencies the API tier served from a
/// static list until this table existed.
/// </summary>
/// <remarks>
/// The seed keeps each agency's identifier from that list. Every
/// <c>agency_id</c> already written — on a report, an exposure or a share —
/// was written against those identifiers, so keeping them is what lets the
/// existing rows go on naming the same agency without a data migration. The
/// names are the ones <c>../nedss/docs/functional/epitrax-cmr-workflow.md</c>
/// records from the legacy creation screen; no short names are recorded there,
/// so none are seeded.
/// </remarks>
internal sealed class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.ToTable("agencies");
        builder.HasKey(agency => agency.Id);

        builder.Property(agency => agency.Id).HasColumnName("id");
        builder.Property(agency => agency.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(agency => agency.ShortName)
            .HasColumnName("short_name")
            .HasMaxLength(50);
        builder.Property(agency => agency.NotificationEmails)
            .HasColumnName("notification_emails")
            .HasMaxLength(1000);

        builder.HasData(
            new Agency { Id = 1, Name = "Bear River" },
            new Agency { Id = 2, Name = "Central Utah" },
            new Agency { Id = 3, Name = "Davis County" },
            new Agency { Id = 4, Name = "Out of State" },
            new Agency { Id = 5, Name = "Salt Lake County" },
            new Agency { Id = 6, Name = "San Juan County" },
            new Agency { Id = 7, Name = "Southeastern Utah" },
            new Agency { Id = 8, Name = "Southwest Utah" },
            new Agency { Id = 9, Name = "Summit County" },
            new Agency { Id = 10, Name = "Tooele County" },
            new Agency { Id = 11, Name = "TriCounty" },
            new Agency { Id = 12, Name = "Utah County" },
            new Agency { Id = 13, Name = "Utah Navajo" },
            new Agency { Id = 14, Name = "Utah State" },
            new Agency { Id = 15, Name = "Wasatch County" },
            new Agency { Id = 16, Name = "Weber-Morgan" });
    }
}
