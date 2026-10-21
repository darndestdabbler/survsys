using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the vocabulary endpoints refuse before the database is asked: each
/// table's row, and a condition's rules.
/// </summary>
public class VocabularyEndpointsTests
{
    [Fact]
    public void WhyNotAMasterLoincAcceptsOneAndRefusesABlankOrLongCode()
    {
        Assert.Null(VocabularyEndpoints.WhyNotAMasterLoinc(new VocabMasterLoincRow(0, "11585-7", null, 1, null, false)));
        Assert.Contains("loinc", VocabularyEndpoints.WhyNotAMasterLoinc(new VocabMasterLoincRow(0, " ", null, 1, null, false)));
        Assert.Contains("longer", VocabularyEndpoints.WhyNotAMasterLoinc(
            new VocabMasterLoincRow(0, new string('1', 21), null, null, null, false)));
    }

    [Fact]
    public void WhyNotAChildLoincNeedsALabAChildCodeAndAMaster()
    {
        Assert.Null(VocabularyEndpoints.WhyNotAChildLoinc(new VocabChildLoincRow(0, 3, "X", "11585-7", null, false)));
        Assert.Contains("labId", VocabularyEndpoints.WhyNotAChildLoinc(new VocabChildLoincRow(0, 0, "X", "11585-7", null, false)));
        Assert.Contains("childLoinc", VocabularyEndpoints.WhyNotAChildLoinc(new VocabChildLoincRow(0, 3, "", "11585-7", null, false)));
        Assert.Contains("masterLoinc", VocabularyEndpoints.WhyNotAChildLoinc(new VocabChildLoincRow(0, 3, "X", "", null, false)));
    }

    [Fact]
    public void WhyNotAnOrganismNeedsASnomedAndAName()
    {
        Assert.Null(VocabularyEndpoints.WhyNotAnOrganism(new VocabMasterOrganismRow(0, "27268008", null, "Salmonella", null)));
        Assert.Contains("snomed", VocabularyEndpoints.WhyNotAnOrganism(new VocabMasterOrganismRow(0, "", null, "Salmonella", null)));
        Assert.Contains("organismName", VocabularyEndpoints.WhyNotAnOrganism(new VocabMasterOrganismRow(0, "1", null, " ", null)));
    }

    [Theory]
    [InlineData("specimen", null)]
    [InlineData("test_result", null)]
    [InlineData("organism", "category")]
    [InlineData("Specimen", "category")]
    public void WhyNotAMasterValueKnowsItsCategories(string category, string? expected)
    {
        var why = VocabularyEndpoints.WhyNotAMasterValue(new VocabMasterValueRow(0, category, "Blood", null));

        if (expected is null)
        {
            Assert.Null(why);
        }
        else
        {
            Assert.Contains(expected, why);
        }
    }

    [Theory]
    [InlineData(3, "organism", "SAL", "27268008", null)]
    [InlineData(3, "unit", "SAL", "x", "category")]
    [InlineData(0, "specimen", "BLD", "Blood", "labId")]
    [InlineData(3, "specimen", "", "Blood", "childCode")]
    [InlineData(3, "specimen", "BLD", "", "master")]
    public void WhyNotAChildValueNeedsALabACategoryACodeAndAMaster(
        int labId, string category, string code, string master, string? expected)
    {
        var why = VocabularyEndpoints.WhyNotAChildValue(new VocabChildValueRow(0, labId, category, code, master));

        if (expected is null)
        {
            Assert.Null(why);
        }
        else
        {
            Assert.Contains(expected, why);
        }
    }

    private static VocabRuleRow Rule(string kind, params VocabRuleConditionRow[] conditions) =>
        new(0, kind, 1, true, false, false, conditions);

    [Fact]
    public void WhyNotRulesAcceptsBothKindsAndNoRulesAtAll()
    {
        Assert.Null(VocabularyEndpoints.WhyNotRules([]));
        Assert.Null(VocabularyEndpoints.WhyNotRules(
        [
            Rule("message_filter", new VocabRuleConditionRow("DaysSinceCollection", "<", "183", null)),
            Rule("case_management",
                new VocabRuleConditionRow("TestResult", "=", "Positive", null),
                new VocabRuleConditionRow("Organism", "<>", "Bacteria", "and")),
        ]));
    }

    [Theory]
    [InlineData("whitelist", "TestResult", "=", null, "kind")]
    [InlineData("case_management", "Age", "=", null, "field")]
    [InlineData("case_management", "TestResult", "==", null, "operator")]
    [InlineData("case_management", "TestResult", "=", "XOR", "and/or")]
    public void WhyNotRulesNamesTheRuleAndWhatIsWrong(
        string kind, string field, string @operator, string? andOr, string expected)
    {
        var why = VocabularyEndpoints.WhyNotRules(
            [Rule("message_filter"), Rule(kind, new VocabRuleConditionRow(field, @operator, "x", andOr))]);

        Assert.Contains("rule 2", why);
        Assert.Contains(expected, why);
    }
}
