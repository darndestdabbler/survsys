using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ParentGuardian"/> onto the <c>parent_guardians</c> table.
/// </summary>
/// <remarks>
/// One foreign key and not two: <see cref="ParentGuardian.ChildId"/> is
/// required and cascades, because the row is about that person and goes with
/// them, while <see cref="ParentGuardian.ParentId"/> is left as a bare column
/// for the reason the entity gives.
/// </remarks>
internal sealed class ParentGuardianConfiguration : IEntityTypeConfiguration<ParentGuardian>
{
    public void Configure(EntityTypeBuilder<ParentGuardian> builder)
    {
        builder.ToTable("parent_guardians");
        builder.HasKey(guardian => guardian.Id);

        builder.Property(guardian => guardian.Id).HasColumnName("id");
        builder.Property(guardian => guardian.ChildId).HasColumnName("child_id");
        builder.Property(guardian => guardian.ParentId).HasColumnName("parent_id");
        builder.Property(guardian => guardian.Name)
            .HasColumnName("parent_guardian")
            .HasMaxLength(400);

        builder.HasOne<Person>()
            .WithMany(person => person.Guardians)
            .HasForeignKey(guardian => guardian.ChildId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
