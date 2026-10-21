namespace Survsys.Messaging;

/// <summary>
/// Expands a dataset repeating group over an EpiTrax record: for each row of
/// the group's dataset it formats the group's PHIN variables once, producing
/// one instance per row so a variable appears once per lab, vaccine, treatment
/// or travel record.
/// </summary>
/// <remarks>
/// The dataset repeating-group processing of the legacy NMI XML generation
/// described in <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, kept
/// to the four datasets the session plan names. Each row is formatted against
/// the case's core fields overlaid with that row's fields, as the legacy
/// "single current object" record does, and tagged with an incrementing
/// instance number. A dataset with no rows yields no instances; a dataset name
/// that is not one of the five supported ones is rejected rather than silently
/// producing nothing.
/// </remarks>
public sealed class RepeatingGroupProcessor
{
    private static readonly IReadOnlySet<string> SupportedDataSets =
        new HashSet<string>(StringComparer.Ordinal) { "Lab", "Vaccine", "Treatment", "Travel", HepatitisLabGrouping.DataSet };

    /// <summary>
    /// Expands <paramref name="group"/> over <paramref name="record"/>, one
    /// instance per dataset row.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The group's dataset is not one of <c>Lab</c>, <c>Vaccine</c>, <c>HepLab</c>,
    /// <c>Treatment</c> or <c>Travel</c>.
    /// </exception>
    public IReadOnlyList<RepeatingGroupInstance> Process(RepeatingGroup group, EpiTraxRecord record)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(record);

        if (!SupportedDataSets.Contains(group.DataSet))
        {
            throw new ArgumentException(
                $"'{group.DataSet}' is not a supported repeating dataset; expected one of "
                    + $"{string.Join(", ", SupportedDataSets)}.",
                nameof(group));
        }

        var instances = new List<RepeatingGroupInstance>();
        var instanceNumber = 0;
        foreach (var row in record.Dataset(group.DataSet))
        {
            instanceNumber++;
            var rowRecord = Merge(record, row);
            var values = group.Variables
                .Select(variable => new RepeatingGroupValue(
                    variable.PhinVar,
                    variable.Formatter.Format(variable.RuleSet, rowRecord, group.DataSet)))
                .ToList();
            instances.Add(new RepeatingGroupInstance(instanceNumber, values));
        }

        return instances;
    }

    private static EpiTraxRecord Merge(EpiTraxRecord record, DatasetRow row)
    {
        var merged = new Dictionary<string, string?>();
        foreach (var field in record.Fields)
        {
            merged[field.Key] = field.Value;
        }

        foreach (var field in row.Fields)
        {
            merged[field.Key] = field.Value;
        }

        return new EpiTraxRecord(merged);
    }
}

/// <summary>
/// One expansion of a repeating group for a single dataset row: the instance
/// number the row's variables are tagged with, and the formatted value of each.
/// </summary>
/// <param name="InstanceNumber">The row's instance number, starting at one.</param>
/// <param name="Values">The formatted value of each of the group's variables for this row.</param>
public sealed record RepeatingGroupInstance(
    int InstanceNumber,
    IReadOnlyList<RepeatingGroupValue> Values);

/// <summary>
/// One PHIN variable's formatted value within a repeating-group instance.
/// </summary>
/// <param name="PhinVar">The variable's identifier.</param>
/// <param name="Formatted">The formatted results the variable's ruleset produced for this row.</param>
public sealed record RepeatingGroupValue(
    string PhinVar,
    IReadOnlyList<RuleSetFormattedData> Formatted);
