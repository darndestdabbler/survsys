namespace Survsys.Messaging;

/// <summary>
/// A raw HL7 v2 message read as ER7 text, segment by segment, so a
/// <see cref="ConformanceProfile"/> can read any element of it by its path —
/// including the elements <see cref="OruR01Parser"/> does not keep.
/// </summary>
/// <remarks>
/// Deliberately minimal: it splits the text <see cref="OruR01Parser"/>
/// preprocesses on the separators MSH-1 and MSH-2 declare and returns the
/// text an element holds, its lower-level separators intact and its escape
/// sequences unread. It knows no segment's structure and no group, so a path
/// reads every occurrence of its segment anywhere in the message.
/// </remarks>
public sealed class Er7Message
{
    private readonly char fieldSeparator;
    private readonly char componentSeparator;
    private readonly char repetitionSeparator;
    private readonly char subcomponentSeparator;

    private Er7Message(IReadOnlyList<string> segments)
    {
        Segments = segments;

        var msh = segments[0];
        fieldSeparator = msh[3];
        var encoding = msh[4..].Split(fieldSeparator)[0];
        componentSeparator = encoding.Length > 0 ? encoding[0] : '^';
        repetitionSeparator = encoding.Length > 1 ? encoding[1] : '~';
        subcomponentSeparator = encoding.Length > 3 ? encoding[3] : '&';
    }

    /// <summary>The message's segments, in message order, each as its text.</summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>
    /// Reads <paramref name="raw"/> as ER7. Throws <see cref="Hl7ParseException"/>
    /// when, once preprocessed, it does not begin with an MSH segment.
    /// </summary>
    public static Er7Message Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var segments = OruR01Parser.Preprocess(raw).Split('\r');
        if (!segments[0].StartsWith("MSH", StringComparison.Ordinal) || segments[0].Length < 8)
        {
            throw new Hl7ParseException("The message does not begin with an MSH segment.");
        }

        return new Er7Message(segments);
    }

    /// <summary>
    /// The text <paramref name="path"/> reads from every occurrence of its
    /// segment and every repetition of its field, in message order: an empty
    /// string where an occurrence leaves the element empty, and none at all
    /// where the segment does not occur. A path is a segment (<c>NK1</c>), a
    /// field (<c>ORC-24</c>), a component (<c>PID-3.4</c>) or a subcomponent
    /// (<c>PID-3.4.2</c>); a segment alone reads each occurrence's whole text.
    /// </summary>
    public IReadOnlyList<string> Values(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var (segment, position) = path.IndexOf('-') is var dash and >= 0
            ? (path[..dash], path[(dash + 1)..].Split('.').Select(int.Parse).ToArray())
            : (path, []);

        var occurrences = Segments.Where(text =>
            text.Length >= segment.Length
            && text.StartsWith(segment, StringComparison.Ordinal)
            && (text.Length == segment.Length || text[segment.Length] == fieldSeparator));

        if (position.Length == 0)
        {
            return occurrences.ToList();
        }

        return occurrences.SelectMany(text => Read(segment, text, position)).ToList();
    }

    private IEnumerable<string> Read(string segment, string text, int[] position)
    {
        var fields = text.Split(fieldSeparator);
        var field = position[0];

        // MSH-1 is the field separator itself, so MSH's fields sit one place
        // earlier in the split than every other segment's; MSH-2 holds the
        // encoding characters and is never split.
        var isMsh = segment == "MSH";
        if (isMsh && field == 1)
        {
            return [fieldSeparator.ToString()];
        }

        var index = isMsh ? field - 1 : field;
        var value = index < fields.Length ? fields[index] : string.Empty;
        if (isMsh && field == 2)
        {
            return [value];
        }

        return value
            .Split(repetitionSeparator)
            .Select(repetition => Part(Part(repetition, componentSeparator, position, 1), subcomponentSeparator, position, 2));
    }

    private static string Part(string value, char separator, int[] position, int level)
    {
        if (position.Length <= level)
        {
            return value;
        }

        var parts = value.Split(separator);
        var index = position[level] - 1;
        return index < parts.Length ? parts[index] : string.Empty;
    }
}
