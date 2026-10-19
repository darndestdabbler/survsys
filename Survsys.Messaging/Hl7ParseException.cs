namespace Survsys.Messaging;

/// <summary>
/// Thrown when a message handed to <see cref="OruR01Parser"/> is not a
/// well-formed ORU^R01: the encoding is unparseable, or the message is of
/// some other type. The parser refuses such a message rather than returning a
/// half-filled result, because a lab result built from half a message is worse
/// than no result at all.
/// </summary>
public sealed class Hl7ParseException : Exception
{
    public Hl7ParseException(string message)
        : base(message)
    {
    }

    public Hl7ParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
