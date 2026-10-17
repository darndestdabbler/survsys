using Survsys.Contracts;

namespace Survsys.Messaging;

/// <summary>
/// What <see cref="OruR01Parser"/> makes of an ORU^R01 lab-result message: the
/// person the result is about and the specimen reported against them, each as
/// the Survsys.Contracts intake record the rest of the system already takes.
/// The two are returned together because one message carries both, and the
/// caller lands the person before the specimen that references them.
/// </summary>
/// <param name="Person">
/// The person from the message's PID segment, as a <see cref="CreatePersonRequest"/>.
/// </param>
/// <param name="Lab">
/// The specimen, its tests and their results from the message's OBR, OBX and
/// SPM segments, as a <see cref="CreateLabRequest"/>.
/// </param>
public record OruR01ParseResult(CreatePersonRequest Person, CreateLabRequest Lab);
