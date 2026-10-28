using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Provider"/> onto the <c>providers</c> table, under the legacy
/// column names.
/// </summary>
/// <remarks>
/// The provider type is a real foreign key to <c>code_values</c>: nothing
/// deletes a code, which is retired by clearing its active flag, so the key
/// never stands in the way of the code lists' administration. The last name
/// is indexed because the registry is searched by it.
/// </remarks>
internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    private const int NameLength = 100;

    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("providers");
        builder.HasKey(provider => provider.Id);

        builder.Property(provider => provider.Id).HasColumnName("id");
        builder.Property(provider => provider.ProviderTypeId).HasColumnName("provider_type_id");
        builder.Property(provider => provider.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(NameLength);
        builder.Property(provider => provider.MiddleName)
            .HasColumnName("middle_name")
            .HasMaxLength(NameLength);
        builder.Property(provider => provider.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(NameLength)
            .IsRequired();
        builder.Property(provider => provider.Phone)
            .HasColumnName("phone")
            .HasMaxLength(30);
        builder.Property(provider => provider.Email)
            .HasColumnName("email")
            .HasMaxLength(200);
        builder.Property(provider => provider.Active).HasColumnName("active");

        builder.HasIndex(provider => provider.LastName);

        builder.HasOne<CodeValue>()
            .WithMany()
            .HasForeignKey(provider => provider.ProviderTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
