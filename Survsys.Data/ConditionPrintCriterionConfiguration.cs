using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ConditionPrintCriterion"/> onto the
/// <c>nmi_condition_print_criteria</c> table, under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the condition and status, so a status is notifiable for a condition once;
/// deleting the condition cascades to its criteria.
/// </remarks>
internal sealed class ConditionPrintCriterionConfiguration
    : IEntityTypeConfiguration<ConditionPrintCriterion>
{
    private const int CaseStatusLength = 2;

    public void Configure(EntityTypeBuilder<ConditionPrintCriterion> builder)
    {
        builder.ToTable("nmi_condition_print_criteria");
        builder.HasKey(criterion => criterion.Id);

        builder.Property(criterion => criterion.Id).HasColumnName("id");
        builder.Property(criterion => criterion.ConditionId).HasColumnName("condition_id");
        builder.Property(criterion => criterion.CaseStatus)
            .HasColumnName("case_status")
            .HasMaxLength(CaseStatusLength)
            .IsRequired();

        builder.HasIndex(criterion => new { criterion.ConditionId, criterion.CaseStatus }).IsUnique();

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(criterion => criterion.ConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
