using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="StagedMessageResult"/> onto the <c>system_message_results</c>
/// table, column by column.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. A result is a detail
/// of its staged message and cascades with it. The text columns are bounded at
/// what an HL7 field of their kind carries; the result value is the widest,
/// because a narrative result is as long as a lab writes it.
/// </remarks>
internal sealed class StagedMessageResultConfiguration : IEntityTypeConfiguration<StagedMessageResult>
{
    private const int CodeLength = 50;
    private const int TextLength = 200;

    public void Configure(EntityTypeBuilder<StagedMessageResult> builder)
    {
        builder.ToTable("system_message_results");
        builder.HasKey(result => result.Id);

        builder.Property(result => result.Id).HasColumnName("id");
        builder.Property(result => result.StagedMessageId).HasColumnName("system_message_id");
        builder.Property(result => result.Sequence).HasColumnName("sequence");
        builder.Property(result => result.ObservationCode)
            .HasColumnName("observation_code")
            .HasMaxLength(CodeLength);
        builder.Property(result => result.ObservationName)
            .HasColumnName("observation_name")
            .HasMaxLength(TextLength);
        builder.Property(result => result.ResultValue)
            .HasColumnName("result_value")
            .HasMaxLength(2000);
        builder.Property(result => result.CodedResult)
            .HasColumnName("coded_result")
            .HasMaxLength(TextLength);
        builder.Property(result => result.Units)
            .HasColumnName("units")
            .HasMaxLength(CodeLength);
        builder.Property(result => result.ReferenceRange)
            .HasColumnName("reference_range")
            .HasMaxLength(CodeLength);
        builder.Property(result => result.AbnormalFlag)
            .HasColumnName("abnormal_flag")
            .HasMaxLength(10);
        builder.Property(result => result.ResultStatus)
            .HasColumnName("result_status")
            .HasMaxLength(10);
        builder.Property(result => result.MasterLoinc)
            .HasColumnName("master_loinc")
            .HasMaxLength(CodeLength);
        builder.Property(result => result.ConditionId).HasColumnName("condition_id");
        builder.Property(result => result.OrganismId).HasColumnName("organism_id");
        builder.Property(result => result.Interpretation)
            .HasColumnName("interpretation")
            .HasMaxLength(TextLength);
    }
}
