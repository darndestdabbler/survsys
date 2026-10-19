// The input the ORU^R01 generator reads: a notification message's header and
// the PHIN variable values already resolved for it, as plain in-memory records.
// None of these is a Survsys.Data entity, and this module reaches for no
// DbContext: the generator is handed everything a message is built from, so the
// messaging module keeps the fence against the database the architecture test
// asserts. Projecting a real case and its formatted rulesets into these records
// is the caller's to do, in a tier that may see both. The shapes follow the
// legacy NMI XML header and PHIN-variable structure described in
// ../nedss/docs/business-logic/NMI_XML_GENERATION.md and the PHIN-variable to
// HL7-field mapping in ../nedss/docs/business-logic/HL7_FIELD_MAPPINGS.md §2.2,
// kept to what generating the message needs.
namespace Survsys.Messaging;

/// <summary>
/// A three-part HL7 identifier — the namespace, and the universal name and type
/// that qualify it — as the MSH application and facility fields carry one.
/// </summary>
/// <remarks>
/// The namespace is the identifier proper and is always present; the name and
/// type are the universal-id pair that qualifies it, left null when the header
/// names only the namespace. The same shape serves the sending and receiving
/// application and facility, because MSH-3 through MSH-6 are all this datatype.
/// </remarks>
/// <param name="Namespace">The identifier's namespace id (MSH-n.1), always present.</param>
/// <param name="Name">The universal id that qualifies it (MSH-n.2), where set.</param>
/// <param name="Type">The universal id type (MSH-n.3), where set.</param>
public sealed record NotificationIdentifier(string Namespace, string? Name, string? Type);

/// <summary>
/// The header an ORU^R01 notification is built from: everything the MSH segment
/// carries, so one input yields one deterministic message.
/// </summary>
/// <remarks>
/// The sending time and the control id are the header's rather than the clock's
/// and a freshly generated value, so generating the same input twice produces
/// the same message — which is what lets a test assert a generated message
/// against a fixture byte for byte. The message type (MSH-9) is not here because
/// it is fixed to <c>ORU^R01^ORU_R01</c> by the generator.
/// </remarks>
/// <param name="SendingApp">The sending application (MSH-3).</param>
/// <param name="SendingFacility">The sending facility (MSH-4).</param>
/// <param name="ReceivingApp">The receiving application (MSH-5).</param>
/// <param name="ReceivingFacility">The receiving facility (MSH-6).</param>
/// <param name="SendingTime">The date and time the message is sent (MSH-7).</param>
/// <param name="ProcessingId">The processing id (MSH-11), for example <c>P</c> or <c>T</c>.</param>
/// <param name="Version">The HL7 version id (MSH-12), for example <c>2.5.1</c>.</param>
/// <param name="ProfileId">The message profile identifier (MSH-21).</param>
/// <param name="ControlId">The message control id (MSH-10), unique to this message.</param>
public sealed record NotificationHeader(
    NotificationIdentifier SendingApp,
    NotificationIdentifier SendingFacility,
    NotificationIdentifier ReceivingApp,
    NotificationIdentifier ReceivingFacility,
    DateTimeOffset SendingTime,
    string ProcessingId,
    string Version,
    string ProfileId,
    string ControlId);

/// <summary>
/// One PHIN variable resolved for a notification: its identity, the value it
/// carries and, where the value is coded, its code and code system.
/// </summary>
/// <remarks>
/// This is the output of the ruleset evaluation the generator consumes, not the
/// evaluation itself: the caller has already formatted the value, so the
/// generator only places it. A variable the generator maps to a named segment
/// field (the DEM demographic variables, the death date, the result status) is
/// placed there; every other variable becomes one OBX segment, its
/// <see cref="PhinVar"/> and <see cref="Name"/> the observation identifier and
/// its <see cref="ValueType"/> the OBX value type. <see cref="SubId"/> is the
/// repeating-group instance a variable belongs to — zero when it does not
/// repeat — so a variable that appears once per lab, vaccine, treatment or
/// travel row keeps its rows apart in OBX-4.
/// </remarks>
/// <param name="PhinVar">The PHIN variable identifier, for example <c>DEM115</c>.</param>
/// <param name="Name">The variable's display name, where set.</param>
/// <param name="ValueType">The HL7 value type the OBX carries (OBX-2), for example <c>ST</c> or <c>CWE</c>.</param>
/// <param name="Value">The formatted value, where set.</param>
/// <param name="Code">The coded value, where the value resolves to a code.</param>
/// <param name="CodeSystemOid">The OID of the code system <see cref="Code"/> belongs to, where set.</param>
/// <param name="OtherSpecified">The "other, specify" text the value carries, where set.</param>
/// <param name="SubId">The repeating-group instance this value belongs to (OBX-4), zero when it does not repeat.</param>
public sealed record NotificationValue(
    string PhinVar,
    string? Name,
    string ValueType,
    string? Value,
    string? Code,
    string? CodeSystemOid,
    string? OtherSpecified,
    int SubId);

/// <summary>
/// Everything the generator needs to build one ORU^R01 notification: its
/// header, the guide profile it is exported under, and its resolved PHIN
/// variable values in the order they are placed.
/// </summary>
/// <param name="Header">The header the MSH segment is built from.</param>
/// <param name="GuideProfile">The notification guide profile this message is generated for.</param>
/// <param name="Values">The resolved PHIN variable values, in the order they are placed.</param>
public sealed record OruR01GenerationInput(
    NotificationHeader Header,
    string GuideProfile,
    IReadOnlyList<NotificationValue> Values);
