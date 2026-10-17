using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RolePrivilege"/> onto the <c>role_privileges</c> table, keyed by
/// role and privilege, seeded with every privilege for the Administrator role.
/// </summary>
/// <remarks>
/// The seeded names are written out because this module references no sibling
/// and so cannot read <c>Survsys.Contracts.Privileges</c>; they are that
/// catalogue's four names, and a privilege added to it is given to the
/// Administrator by the migration that adds it.
/// </remarks>
internal sealed class RolePrivilegeConfiguration : IEntityTypeConfiguration<RolePrivilege>
{
    public void Configure(EntityTypeBuilder<RolePrivilege> builder)
    {
        builder.ToTable("role_privileges");
        builder.HasKey(rolePrivilege => new { rolePrivilege.RoleId, rolePrivilege.Privilege });

        builder.Property(rolePrivilege => rolePrivilege.RoleId).HasColumnName("role_id");
        builder.Property(rolePrivilege => rolePrivilege.Privilege)
            .HasColumnName("privilege")
            .HasMaxLength(50);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rolePrivilege => rolePrivilege.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new RolePrivilege { RoleId = Role.AdministratorId, Privilege = "view_cases" },
            new RolePrivilege { RoleId = Role.AdministratorId, Privilege = "edit_cases" },
            new RolePrivilege { RoleId = Role.AdministratorId, Privilege = "manage_reference_data" },
            new RolePrivilege { RoleId = Role.AdministratorId, Privilege = "manage_users" });
    }
}
