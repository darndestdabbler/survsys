namespace Survsys.Contracts;

/// <summary>
/// One of a condition's lab overrides, as
/// <c>/api/conditions/{id}/lab-overrides</c> reads and replaces them: an
/// organism or test type added to the labs the condition's notification
/// carries, or taken out of them.
/// </summary>
/// <param name="Kind"><c>organism</c> or <c>test_type</c>.</param>
/// <param name="ItemId">The master organism, or the <c>test_type</c> code value, overridden.</param>
/// <param name="Include">True to add the item to the condition's defaults, false to take it out.</param>
public record ConditionLabOverrideRow(string Kind, int ItemId, bool Include);
