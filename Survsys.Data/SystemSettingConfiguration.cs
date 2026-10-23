using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="SystemSetting"/> onto the <c>system_setting</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_setting");
        builder.HasKey(setting => setting.Name);

        builder.Property(setting => setting.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(setting => setting.Value).HasColumnName("value").IsRequired();
        builder.Property(setting => setting.Description).HasColumnName("description").HasMaxLength(500);
    }
}
