namespace Survsys.Messaging;

/// <summary>
/// An HL7 usage code, as an implementation guide states one for an element.
/// </summary>
public enum Usage
{
    /// <summary>Required: the element must be valued.</summary>
    R,

    /// <summary>Required but may be empty: valued where the sender has it.</summary>
    RE,

    /// <summary>Optional.</summary>
    O,

    /// <summary>Not supported: the element is not to be sent.</summary>
    X,
}

/// <summary>
/// The conditional usage codes, <c>C(a/b)</c>: the first usage where the
/// condition predicate holds, the second where it does not.
/// </summary>
public enum ConditionalUsage
{
    /// <summary><c>C(R/X)</c>.</summary>
    RequiredElseNotSupported,

    /// <summary><c>C(RE/X)</c>.</summary>
    RequiredOrEmptyElseNotSupported,

    /// <summary><c>C(R/RE)</c>.</summary>
    RequiredElseRequiredOrEmpty,

    /// <summary><c>C(R/O)</c>.</summary>
    RequiredElseOptional,
}

/// <summary>How a failed check is recorded on the message.</summary>
public enum ConformanceSeverity
{
    /// <summary>The message is held as an exception.</summary>
    Exception,

    /// <summary>The message proceeds, with the failure recorded against it.</summary>
    Warning,
}

/// <summary>One check a message failed: the element, how it is recorded, and why.</summary>
public sealed record ConformanceFailure(string Element, ConformanceSeverity Severity, string Detail);

/// <summary>
/// One element's usage in a profile: the usage that applies where
/// <paramref name="Predicate"/> holds or there is none, the usage where it does
/// not, and the check a valued element must pass, described by
/// <paramref name="CheckDescription"/>.
/// </summary>
/// <param name="Element">The element's path, as <see cref="Er7Message.Values"/> reads it.</param>
public sealed record ConformanceRule(
    string Element,
    Usage WhenTrue,
    Usage WhenFalse,
    Func<Er7Message, bool>? Predicate,
    Func<string, bool>? Check,
    string? CheckDescription)
{
    /// <summary>A rule of a plain usage code, with an optional check of the element's value.</summary>
    public static ConformanceRule Of(
        string element, Usage usage, Func<string, bool>? check = null, string? checkDescription = null) =>
        new(element, usage, usage, Predicate: null, check, checkDescription);

    /// <summary>A rule of a conditional usage code, its predicate read over the whole message.</summary>
    public static ConformanceRule Conditional(
        string element,
        ConditionalUsage usage,
        Func<Er7Message, bool> predicate,
        Func<string, bool>? check = null,
        string? checkDescription = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var (whenTrue, whenFalse) = usage switch
        {
            ConditionalUsage.RequiredElseNotSupported => (Usage.R, Usage.X),
            ConditionalUsage.RequiredOrEmptyElseNotSupported => (Usage.RE, Usage.X),
            ConditionalUsage.RequiredElseRequiredOrEmpty => (Usage.R, Usage.RE),
            ConditionalUsage.RequiredElseOptional => (Usage.R, Usage.O),
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null),
        };

        return new(element, whenTrue, whenFalse, predicate, check, checkDescription);
    }
}

/// <summary>
/// A conformance profile: the usage an implementation guide gives each element
/// it constrains, checked against a raw message.
/// </summary>
/// <remarks>
/// A failed check is an exception or a warning by its element's usage: an R
/// element empty or failing its check is an exception; an RE element empty,
/// an RE or O element failing its check, or an X element valued, is a
/// warning. An O element left empty fails nothing. An element is empty when
/// its segment does not occur, or when any occurrence leaves it empty, and
/// each rule reports at most its first failure.
/// </remarks>
public sealed record ConformanceProfile(string Name, IReadOnlyList<ConformanceRule> Rules)
{
    /// <summary>
    /// The checks <paramref name="raw"/> fails, in the order of
    /// <see cref="Rules"/>. Throws <see cref="Hl7ParseException"/> where the
    /// text is not ER7.
    /// </summary>
    public IReadOnlyList<ConformanceFailure> Check(string raw)
    {
        var message = Er7Message.Parse(raw);
        var failures = new List<ConformanceFailure>();

        foreach (var rule in Rules)
        {
            var usage = rule.Predicate is null || rule.Predicate(message) ? rule.WhenTrue : rule.WhenFalse;
            var values = message.Values(rule.Element);
            var empty = values.Count == 0 || values.Any(string.IsNullOrEmpty);

            if (usage == Usage.X)
            {
                if (values.Any(value => value.Length > 0))
                {
                    failures.Add(new(rule.Element, ConformanceSeverity.Warning, $"{rule.Element} is not supported but is valued."));
                }

                continue;
            }

            if (empty && usage == Usage.R)
            {
                failures.Add(new(rule.Element, ConformanceSeverity.Exception, $"{rule.Element} is required but is empty."));
                continue;
            }

            if (empty && usage == Usage.RE)
            {
                failures.Add(new(rule.Element, ConformanceSeverity.Warning, $"{rule.Element} is empty."));
                continue;
            }

            if (rule.Check is not null
                && values.FirstOrDefault(value => value.Length > 0 && !rule.Check(value)) is { } invalid)
            {
                failures.Add(new(
                    rule.Element,
                    usage == Usage.R ? ConformanceSeverity.Exception : ConformanceSeverity.Warning,
                    $"{rule.Element} '{invalid}' is not {rule.CheckDescription ?? "valid"}."));
            }
        }

        return failures;
    }
}
