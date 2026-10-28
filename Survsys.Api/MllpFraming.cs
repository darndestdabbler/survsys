using System.Globalization;
using System.Text;

namespace Survsys.Api;

/// <summary>
/// The Minimal Lower Layer Protocol's framing and the HL7 acknowledgement each
/// framed message is answered with: a message travels as
/// <c>&lt;VT&gt;message&lt;FS&gt;&lt;CR&gt;</c>, and the answer is an ACK whose
/// MSA-1 says whether it was accepted (<see cref="Accept"/>), held as an error
/// (<see cref="Error"/>) or could not be taken at all (<see cref="Reject"/>).
/// </summary>
/// <remarks>
/// Pure and static, so the framing and the ACK are proven without a socket;
/// <see cref="MllpListener"/> is the transport around them.
/// </remarks>
public static class MllpFraming
{
    /// <summary>The start-of-block byte, vertical tab.</summary>
    public const char StartBlock = '\u000b';

    /// <summary>The end-of-block byte, file separator, followed by a carriage return.</summary>
    public const char EndBlock = '\u001c';

    /// <summary>MSA-1: the message was accepted and queued for review.</summary>
    public const string Accept = "AA";

    /// <summary>MSA-1: the message was received but held as an exception.</summary>
    public const string Error = "AE";

    /// <summary>MSA-1: the message could not be received at all.</summary>
    public const string Reject = "AR";

    private const string DefaultEncodingCharacters = "^~\\&";

    /// <summary>Wraps one message in its MLLP frame.</summary>
    public static string Frame(string message) => $"{StartBlock}{message}{EndBlock}\r";

    /// <summary>
    /// Takes the first whole frame off the front of <paramref name="buffer"/>:
    /// true with the message inside it and what follows it, or false with the
    /// buffer as it was where no whole frame has arrived yet. Anything before
    /// the start block is not part of a message and is dropped.
    /// </summary>
    public static bool TryUnframe(string buffer, out string message, out string remainder)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        message = string.Empty;
        remainder = buffer;

        var start = buffer.IndexOf(StartBlock);
        if (start < 0)
        {
            return false;
        }

        var end = buffer.IndexOf($"{EndBlock}\r", start + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        message = buffer[(start + 1)..end];
        remainder = buffer[(end + 2)..];
        return true;
    }

    /// <summary>
    /// The ACK answering <paramref name="raw"/>: its MSH with the sender and
    /// receiver swapped, and an MSA carrying <paramref name="code"/>, the
    /// message's control id (MSH-10) and <paramref name="text"/> where there is
    /// one. A message with no readable MSH is answered from defaults, with no
    /// control id to echo.
    /// </summary>
    public static string Acknowledgement(string raw, string code, string? text = null)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(code);

        var msh = raw
            .Split('\r', '\n')
            .Select(segment => segment.Trim())
            .FirstOrDefault(segment => segment.StartsWith("MSH", StringComparison.Ordinal) && segment.Length > 3);

        var separator = msh?[3] ?? '|';
        var fields = msh?.Split(separator) ?? [];
        string Field(int number) => fields.Length >= number ? fields[number - 1] : string.Empty;

        var encodingCharacters = Field(2) is { Length: > 0 } declared ? declared : DefaultEncodingCharacters;
        var component = encodingCharacters[0];
        var trigger = Field(9).Split(component).ElementAtOrDefault(1) ?? string.Empty;
        var processingId = Field(11) is { Length: > 0 } processing ? processing : "P";
        var version = Field(12) is { Length: > 0 } declaredVersion ? declaredVersion : "2.5.1";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var controlId = Guid.NewGuid().ToString("N")[..20];

        var s = separator;
        var header = $"MSH{s}{encodingCharacters}{s}{Field(5)}{s}{Field(6)}{s}{Field(3)}{s}{Field(4)}{s}"
            + $"{timestamp}{s}{s}ACK{component}{trigger}{component}ACK{s}{controlId}{s}{processingId}{s}{version}";
        var msa = $"MSA{s}{code}{s}{Field(10)}";

        if (!string.IsNullOrWhiteSpace(text))
        {
            msa += $"{s}{Escape(text, separator, encodingCharacters)}";
        }

        return $"{header}\r{msa}";
    }

    /// <summary>
    /// HL7 v2 escaping of free text, so a reason that carries a delimiter does
    /// not split the field it sits in.
    /// </summary>
    private static string Escape(string text, char separator, string encodingCharacters)
    {
        var escape = encodingCharacters.Length > 2 ? encodingCharacters[2] : '\\';
        var escaped = new StringBuilder(text.Length);

        foreach (var character in text.ReplaceLineEndings(" "))
        {
            var sequence = character == escape ? "E"
                : character == separator ? "F"
                : character == encodingCharacters[0] ? "S"
                : encodingCharacters.Length > 1 && character == encodingCharacters[1] ? "R"
                : encodingCharacters.Length > 3 && character == encodingCharacters[3] ? "T"
                : null;

            if (sequence is null)
            {
                escaped.Append(character);
            }
            else
            {
                escaped.Append(escape).Append(sequence).Append(escape);
            }
        }

        return escaped.ToString();
    }
}
