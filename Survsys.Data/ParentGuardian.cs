namespace Survsys.Data;

/// <summary>
/// The parent or guardian of a person: who an investigator speaks to about a
/// child's case.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>parent_guardian</c> table. It carries both a name and a
/// link, because the legacy system allows either: <see cref="Name"/> is the
/// guardian as the Demographic tab's Parent/Guardian field takes them — a name
/// typed into a text box — and <see cref="ParentId"/> is the guardian where
/// they are themselves a person in the system, as they are when a whole
/// household is under investigation.
///
/// <see cref="ChildId"/> is the person this row is about, and the foreign key
/// behind <see cref="Person.Guardians"/>. <see cref="ParentId"/> stays a plain
/// nullable int rather than a second navigation to <see cref="Person"/>: the
/// legacy table lets a guardian be a name without being a person, so most rows
/// have nothing for it to point at, and a navigation whose target is usually
/// absent earns nothing.
///
/// Not to be confused with the legacy <c>person_condition.parent_guardian</c>
/// column, which holds the guardian named on one report rather than the
/// person's own; that column arrives with the case morbidity report entity.
/// </remarks>
public class ParentGuardian
{
    public int Id { get; set; }

    /// <summary>The person whose parent or guardian this is.</summary>
    public int ChildId { get; set; }

    /// <summary>
    /// The guardian, where they are themselves a person in the system. A bare
    /// identifier rather than a navigation, and null for the many rows that
    /// carry only a name.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// The guardian's name as it was written. <c>Name</c> rather than
    /// <c>ParentGuardian</c> over the <c>parent_guardian</c> column, because
    /// C# will not let a property share the name of its own type.
    /// </summary>
    public string? Name { get; set; }
}
