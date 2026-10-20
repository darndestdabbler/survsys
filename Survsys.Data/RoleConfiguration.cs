using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Role"/> onto the <c>roles</c> table, one row per name, seeded
/// with the Administrator role.
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id).HasColumnName("id");
        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(role => role.Name).IsUnique();

        builder.HasData(new Role { Id = Role.AdministratorId, Name = "Administrator" });
    }
}
