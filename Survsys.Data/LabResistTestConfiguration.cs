using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="LabResistTest"/> onto the <c>lab_resist_tests</c> table, column
/// by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>lab_resist_tests</c>, the plural every table here is given.
/// <see cref="LabResistTest.LabId"/> is required where the legacy <c>lab_id</c>
/// is nullable in name only — a susceptibility test with no specimen is a row
/// about nothing — and cascades on delete, as the specimen's tests do: a
/// susceptibility test is a detail of its specimen and goes when it does.
/// </remarks>
internal sealed class LabResistTestConfiguration : IEntityTypeConfiguration<LabResistTest>
{
    public void Configure(EntityTypeBuilder<LabResistTest> builder)
    {
        builder.ToTable("lab_resist_tests");
        builder.HasKey(resist => resist.Id);

        builder.Property(resist => resist.Id).HasColumnName("id");
        builder.Property(resist => resist.LabId).HasColumnName("lab_id");
        builder.Property(resist => resist.OrganismId).HasColumnName("organism_id");
        builder.Property(resist => resist.ResistTestAgentId).HasColumnName("resist_test_agent_id");
        builder.Property(resist => resist.ResistTestResultId).HasColumnName("resist_test_result_id");
        builder.Property(resist => resist.TestDate).HasColumnName("test_date");

        // No navigation on either side. A susceptibility test is a detail of
        // its specimen, so it cascades: deleting the specimen deletes it.
        builder.HasOne<Lab>()
            .WithMany()
            .HasForeignKey(resist => resist.LabId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
