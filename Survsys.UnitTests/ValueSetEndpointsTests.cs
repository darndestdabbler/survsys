using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the value set endpoints refuse before the database is asked: a PHIN
/// value set concept or a unit of measure with no value set, code or name, or
/// a field longer than its column.
/// </summary>
public class ValueSetEndpointsTests
{
    [Fact]
    public void WhyNotAPhinValueSetConceptAcceptsOneAndRefusesABlankOrLongField()
    {
        var concept = new PhinValueSetRow(0, "PHVS_YesNoUnknown_CDC", "Y", "Yes", "Y", "2.16.840.1.113883.12.136", null);

        Assert.Null(ValueSetEndpoints.WhyNotAPhinValueSetConcept(concept));
        Assert.Contains("valueSetName", ValueSetEndpoints.WhyNotAPhinValueSetConcept(concept with { ValueSetName = " " }));
        Assert.Contains("conceptCode", ValueSetEndpoints.WhyNotAPhinValueSetConcept(concept with { ConceptCode = "" }));
        Assert.Contains(
            "preferredConceptName", ValueSetEndpoints.WhyNotAPhinValueSetConcept(concept with { PreferredConceptName = "" }));
        Assert.Contains("longer", ValueSetEndpoints.WhyNotAPhinValueSetConcept(concept with { CdcName = new string('x', 501) }));
    }

    [Fact]
    public void WhyNotAUnitOfMeasureAcceptsOneAndRefusesABlankOrLongField()
    {
        var unit = new PhinUnitOfMeasureRow(0, "PHVS_AgeUnit_UCUM", "a", "year", "a", "2.16.840.1.113883.6.8");

        Assert.Null(ValueSetEndpoints.WhyNotAUnitOfMeasure(unit));
        Assert.Contains("conceptCode", ValueSetEndpoints.WhyNotAUnitOfMeasure(unit with { ConceptCode = " " }));
        Assert.Contains("longer", ValueSetEndpoints.WhyNotAUnitOfMeasure(unit with { Hl7Code = new string('x', 51) }));
    }
}
