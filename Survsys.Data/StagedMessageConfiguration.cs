using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="StagedMessage"/> onto the legacy <c>system_messages</c> table,
/// column by column and under the legacy names where there is one.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly, as it
/// finds the others. The raw message and the exception detail are unbounded
/// text: a lab message is as long as it is, and an exception detail carries
/// whatever the parser said. The review status is a short code held against
/// <see cref="MessageReviewStatusRules"/>, the length a code needs and no more.
/// The parsed fields are nullable, because a message that failed to parse has
/// none, and bounded at what their HL7 fields carry. The format defaults to
/// <c>HL7v2</c> in the database too, so the rows staged before it was recorded
/// read as the HL7 v2 messages they were. The results cascade with their
/// message.
/// </remarks>
internal sealed class StagedMessageConfiguration : IEntityTypeConfiguration<StagedMessage>
{
    private const int CodeLength = 20;
    private const int SourceLength = 200;
    private const int NameLength = 100;
    private const int ShortLength = 50;

    public void Configure(EntityTypeBuilder<StagedMessage> builder)
    {
        builder.ToTable("system_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).HasColumnName("id");
        builder.Property(message => message.RawMessage)
            .HasColumnName("message")
            .IsRequired();
        builder.Property(message => message.Source)
            .HasColumnName("source")
            .HasMaxLength(SourceLength);
        builder.Property(message => message.ReceivedAt).HasColumnName("received_at");
        builder.Property(message => message.ReviewStatus)
            .HasColumnName("final_status")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(message => message.QaFlagged).HasColumnName("qa_flag");
        builder.Property(message => message.ExceptionDetail).HasColumnName("exception_detail");

        builder.Property(message => message.MessageFormat)
            .HasColumnName("message_format")
            .HasMaxLength(CodeLength)
            .HasDefaultValue("HL7v2")
            .IsRequired();
        builder.Property(message => message.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(CodeLength);
        builder.Property(message => message.SendingFacility)
            .HasColumnName("sending_facility")
            .HasMaxLength(NameLength);
        builder.Property(message => message.PatientLastName)
            .HasColumnName("patient_last_name")
            .HasMaxLength(NameLength);
        builder.Property(message => message.PatientFirstName)
            .HasColumnName("patient_first_name")
            .HasMaxLength(NameLength);
        builder.Property(message => message.PatientMiddleName)
            .HasColumnName("patient_middle_name")
            .HasMaxLength(NameLength);
        builder.Property(message => message.PatientBirthDate).HasColumnName("patient_birth_date");
        builder.Property(message => message.PatientSex)
            .HasColumnName("patient_sex")
            .HasMaxLength(CodeLength);
        builder.Property(message => message.PatientStreet)
            .HasColumnName("patient_street")
            .HasMaxLength(SourceLength);
        builder.Property(message => message.PatientCity)
            .HasColumnName("patient_city")
            .HasMaxLength(NameLength);
        builder.Property(message => message.PatientState)
            .HasColumnName("patient_state")
            .HasMaxLength(CodeLength);
        builder.Property(message => message.PatientZip)
            .HasColumnName("patient_zip")
            .HasMaxLength(CodeLength);
        builder.Property(message => message.OrderIndex).HasColumnName("order_index");
        builder.Property(message => message.AccessionNo)
            .HasColumnName("accession_no")
            .HasMaxLength(NameLength);
        builder.Property(message => message.TestCode)
            .HasColumnName("test_code")
            .HasMaxLength(ShortLength);
        builder.Property(message => message.TestName)
            .HasColumnName("test_name")
            .HasMaxLength(SourceLength);
        builder.Property(message => message.ObservedOn).HasColumnName("observed_on");
        builder.Property(message => message.SpecimenSourceCode)
            .HasColumnName("specimen_source_code")
            .HasMaxLength(ShortLength);
        builder.Property(message => message.SpecimenSourceText)
            .HasColumnName("specimen_source_text")
            .HasMaxLength(SourceLength);
        builder.Property(message => message.CollectedOn).HasColumnName("collected_on");
        builder.Property(message => message.ConditionId).HasColumnName("condition_id");
        builder.Property(message => message.AgencyId).HasColumnName("agency_id");
        builder.Property(message => message.AllowNewCase).HasColumnName("allow_new_cmr");
        builder.Property(message => message.AllowUpdateCase).HasColumnName("allow_update_cmr");
        builder.Property(message => message.IsSurveillance).HasColumnName("is_surveillance");

        builder.HasMany(message => message.Results)
            .WithOne()
            .HasForeignKey(result => result.StagedMessageId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
