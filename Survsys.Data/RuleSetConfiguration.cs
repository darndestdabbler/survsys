using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSet"/> onto the <c>rule_set</c> table, column by column and
/// under the legacy names, seeded with the seven ruleset types.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The id is
/// assigned rather than generated, so the seven ids stay stable for the
/// configurations and PHIN variables that refer to a type by id.
/// </remarks>
internal sealed class RuleSetConfiguration : IEntityTypeConfiguration<RuleSet>
{
    public void Configure(EntityTypeBuilder<RuleSet> builder)
    {
        builder.ToTable("rule_set");
        builder.HasKey(ruleSet => ruleSet.Id);

        builder.Property(ruleSet => ruleSet.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(ruleSet => ruleSet.Label)
            .HasColumnName("label")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasData(
            new RuleSet { Id = 1, Label = "staticValue" },
            new RuleSet { Id = 2, Label = "cdcFirstSent" },
            new RuleSet { Id = 3, Label = "cdcDiseaseId" },
            new RuleSet { Id = 4, Label = "fieldValueMap" },
            new RuleSet { Id = 5, Label = "conditionMap" },
            new RuleSet { Id = 6, Label = "notificationStatusResponse" },
            new RuleSet { Id = 7, Label = "repeatingGroup" });
    }
}
