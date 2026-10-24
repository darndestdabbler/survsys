using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the PHIN variable endpoints refuse before the database is asked: a
/// variable with no identifier or description, or a field longer than its
/// column.
/// </summary>
public class PhinVariableEndpointsTests
{
    private static readonly PhinVariableRow Variable =
        new("INV163", "Case Status", null, null, "CWE", 4, 12, "Active", null, "SCT");

    [Fact]
    public void WhyNotAPhinVariableAcceptsOne()
    {
        Assert.Null(PhinVariableEndpoints.WhyNotAPhinVariable(Variable));
    }

    [Fact]
    public void WhyNotAPhinVariableNeedsAnIdentifierAndADescription()
    {
        Assert.Contains("phinVar", PhinVariableEndpoints.WhyNotAPhinVariable(Variable with { PhinVar = " " }));
        Assert.Contains("description", PhinVariableEndpoints.WhyNotAPhinVariable(Variable with { Description = "" }));
    }

    [Fact]
    public void WhyNotAPhinVariableRefusesAFieldLongerThanItsColumn()
    {
        Assert.Contains("longer", PhinVariableEndpoints.WhyNotAPhinVariable(Variable with { PhinVar = new string('X', 51) }));
        Assert.Contains("hl7DataType", PhinVariableEndpoints.WhyNotAPhinVariable(Variable with { Hl7DataType = new string('X', 21) }));
    }
}
