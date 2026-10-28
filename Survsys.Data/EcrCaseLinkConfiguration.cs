using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrCaseLink"/> onto the <c>ecr_case_link</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. A
/// version is linked to a case once, and the link is deleted with its
/// document or its case.
/// </remarks>
internal sealed class EcrCaseLinkConfiguration : IEntityTypeConfiguration<EcrCaseLink>
{
    public void Configure(EntityTypeBuilder<EcrCaseLink> builder)
    {
        builder.ToTable("ecr_case_link");
        builder.HasKey(link => link.Id);

        builder.Property(link => link.Id).HasColumnName("id");
        builder.Property(link => link.EcrDocumentId).HasColumnName("ecr_document_id");
        builder.Property(link => link.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(link => link.CreatedAt).HasColumnName("created_at");

        builder.HasOne(link => link.EcrDocument)
            .WithMany(document => document.CaseLinks)
            .HasForeignKey(link => link.EcrDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(link => link.PersonConditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(link => new { link.EcrDocumentId, link.PersonConditionId }).IsUnique();
        builder.HasIndex(link => link.PersonConditionId);
    }
}
