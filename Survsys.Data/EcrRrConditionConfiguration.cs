using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrRrCondition"/> onto the <c>ecr_rr_condition</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. A kept
/// condition belongs to one RR and is deleted with it. The code system and
/// code are as long as <see cref="EcrConditionMapping"/>'s, which they are
/// looked up in.
/// </remarks>
internal sealed class EcrRrConditionConfiguration : IEntityTypeConfiguration<EcrRrCondition>
{
    public void Configure(EntityTypeBuilder<EcrRrCondition> builder)
    {
        builder.ToTable("ecr_rr_condition");
        builder.HasKey(condition => condition.Id);

        builder.Property(condition => condition.Id).HasColumnName("id");
        builder.Property(condition => condition.EcrDocumentId).HasColumnName("ecr_document_id");
        builder.Property(condition => condition.CodeSystem).HasColumnName("code_system").HasMaxLength(100);
        builder.Property(condition => condition.Code).HasColumnName("code").HasMaxLength(50);
        builder.Property(condition => condition.DisplayName).HasColumnName("display_name").HasMaxLength(500);
        builder.Property(condition => condition.DeterminationCode)
            .HasColumnName("determination_code")
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(condition => condition.ResponsibleAgencyId)
            .HasColumnName("responsible_agency_id")
            .HasMaxLength(300);
        builder.Property(condition => condition.ReportingTimeframe)
            .HasColumnName("reporting_timeframe")
            .HasMaxLength(50);

        builder.HasOne(condition => condition.EcrDocument)
            .WithMany(document => document.RrConditions)
            .HasForeignKey(condition => condition.EcrDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
