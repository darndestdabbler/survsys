using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the form endpoints decide without the database: the list-row wire
/// shape, and what a form to save must be in itself — its name, and the shape
/// of its sections, questions and coded options, the parts a pure validator
/// can judge. Whether the condition exists is the database's to answer and is
/// proven in the integration suite.
/// </summary>
public class FormEndpointsTests
{
    private static SaveFormRequest AForm(params SaveFormSectionRequest[] sections) =>
        new("Gastro supplement", ConditionId: 4, Sections: sections);

    [Fact]
    public void ToResponseCarriesTheFormsOwnColumns()
    {
        var created = new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);
        var form = new Form
        {
            Id = 7,
            Name = "Gastro supplement",
            Description = "Extra questions",
            ConditionId = 4,
            AutoAssign = true,
            CreatedAt = created,
        };

        Assert.Equal(
            new FormResponse(7, "Gastro supplement", 4, null, true, created),
            FormEndpoints.ToResponse.Compile().Invoke(form));
    }

    [Fact]
    public void WhyNotAFormAcceptsANameAndConditionAlone()
    {
        Assert.Null(FormEndpoints.WhyNotAForm(new SaveFormRequest("Gastro supplement", ConditionId: 4)));
    }

    [Fact]
    public void WhyNotAFormAcceptsAWholeTree()
    {
        var request = AForm(
            new SaveFormSectionRequest(
                "Exposure",
                [
                    new SaveFormQuestionRequest("Where did you eat?"),
                    new SaveFormQuestionRequest(
                        "Did you eat shellfish?",
                        AnswerType: FormEndpoints.AnswerTypeCoded,
                        IsRequired: true,
                        Options:
                        [
                            new SaveFormQuestionOptionRequest("Y", "Yes"),
                            new SaveFormQuestionOptionRequest("N", "No"),
                        ]),
                ]));

        Assert.Null(FormEndpoints.WhyNotAForm(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotAFormRefusesAFormWithNoName(string name)
    {
        Assert.Contains("name is required",
            FormEndpoints.WhyNotAForm(new SaveFormRequest(name, ConditionId: 4)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 200",
            FormEndpoints.WhyNotAForm(new SaveFormRequest(new string('a', 201), ConditionId: 4)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesASectionWithNoTitle()
    {
        Assert.Contains("a section has no title",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest("   "))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesAQuestionWithNoText()
    {
        Assert.Contains("a question has no text",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest(
                "Exposure",
                [new SaveFormQuestionRequest("  ")]))),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void WhyNotAFormRefusesAnUnknownAnswerType(int answerType)
    {
        Assert.Contains($"answerType {answerType} is not a known answer type",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest(
                "Exposure",
                [new SaveFormQuestionRequest("A question", AnswerType: answerType)]))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesACodedQuestionWithNoOptions()
    {
        Assert.Contains("a coded question carries no options",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest(
                "Exposure",
                [new SaveFormQuestionRequest("Pick one", AnswerType: FormEndpoints.AnswerTypeCoded)]))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesACodedOptionWithNoCode()
    {
        Assert.Contains("an option has no code",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest(
                "Exposure",
                [
                    new SaveFormQuestionRequest(
                        "Pick one",
                        AnswerType: FormEndpoints.AnswerTypeCoded,
                        Options: [new SaveFormQuestionOptionRequest("  ", "Yes")]),
                ]))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesACodedOptionWithNoLabel()
    {
        Assert.Contains("an option has no label",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest(
                "Exposure",
                [
                    new SaveFormQuestionRequest(
                        "Pick one",
                        AnswerType: FormEndpoints.AnswerTypeCoded,
                        Options: [new SaveFormQuestionOptionRequest("Y", "  ")]),
                ]))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFormRefusesATextQuestionThatCarriesOptions()
    {
        Assert.Contains("a text question carries options",
            FormEndpoints.WhyNotAForm(AForm(new SaveFormSectionRequest(
                "Exposure",
                [
                    new SaveFormQuestionRequest(
                        "Free text",
                        AnswerType: FormEndpoints.AnswerTypeText,
                        Options: [new SaveFormQuestionOptionRequest("Y", "Yes")]),
                ]))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NotAConditionNamesTheConditionAndWhereTheConditionsAre()
    {
        var sentence = FormEndpoints.NotACondition(42);

        Assert.Contains("conditionId 42", sentence, StringComparison.Ordinal);
        Assert.Contains("/api/conditions", sentence, StringComparison.Ordinal);
    }
}
