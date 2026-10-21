namespace Survsys.Messaging;

/// <summary>
/// The seam intake parses through: raw message text in, the
/// <see cref="ParsedMessage"/> intake keeps out. <see cref="OruR01Parser"/> is
/// the HL7 v2 implementation; a CDA parser is a second one, and intake does
/// not change for it.
/// </summary>
public interface IMessageParser
{
    /// <summary>The format this parser reads, as it is recorded on a staged message.</summary>
    string Format { get; }

    /// <summary>
    /// Parse one raw message. Throws <see cref="Hl7ParseException"/> when the
    /// text is not a message this parser can read.
    /// </summary>
    ParsedMessage Parse(string raw);
}
