using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonConditionShare"/> onto the <c>person_condition_shares</c>
/// table, under the legacy-style snake_case names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>person_condition_shares</c>, the plural every table here is given. Unlike
/// <see cref="PersonConditionLabConfiguration"/> the key is an identity of its
/// own rather than the pair of ids, because a share is a record a later session
/// revokes and audits; the pair is held unique by an index instead, so a report
/// is not shared to the same agency twice.
///
/// The report is a foreign key, deleted with the report it shares — a share is a
/// detail of the report's visibility and not a record that outlives it. The
/// agency is a plain column with no foreign key, as
/// <see cref="PersonCondition.AgencyId"/> is: there is no <c>agency</c> table in
/// this schema to point at, and the value is a reference into the stand-in list
/// the API tier serves.
/// </remarks>
internal sealed class PersonConditionShareConfiguration
    : IEntityTypeConfiguration<PersonConditionShare>
{
    public void Configure(EntityTypeBuilder<PersonConditionShare> builder)
    {
        builder.ToTable("person_condition_shares");
        builder.HasKey(share => share.Id);

        builder.Property(share => share.Id).HasColumnName("id");
        builder.Property(share => share.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(share => share.AgencyId).HasColumnName("agency_id");

        // One report is shared to one agency once: the second share of the same
        // pair breaks on this index rather than quietly writing a duplicate.
        builder.HasIndex(share => new { share.PersonConditionId, share.AgencyId }).IsUnique();

        // No navigation on either side. The report is a real foreign key and the
        // share goes when the report does; the agency is a bare reference into
        // the stand-in list, so it has no key to point at.
        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(share => share.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
