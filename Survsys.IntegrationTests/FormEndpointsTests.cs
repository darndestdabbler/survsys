using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The form builder endpoints, against the real database: that a form is built
/// from its whole tree and read back with its sections, questions and coded
/// options in order and its condition named, that a save replaces the tree,
/// that the list comes back newest first with the condition's name and the
/// auto-assign flag, and that a bad shape, an unknown condition and an unknown
/// id are refused.
/// </summary>
/// <remarks>
/// Driven through the endpoints. The forms table is cleared before and after
/// each test, which cascades to the sections, questions and options, so each
/// test starts and ends with no forms; the seeded conditions are left as they
/// are, since the suite shares the one database and nothing here changes them.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class FormEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AFormIsBuiltAndReadBackWholeInOrder()
    {
        await Clear();

        try
        {
            var condition = await ConditionId("Malaria");

            var created = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                AutoAssign: true,
                Description: "Extra questions for gastro cases",
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [
                            new SaveFormQuestionRequest("Where did you eat?", HelpText: "A place name"),
                            new SaveFormQuestionRequest(
                                "Did you eat shellfish?",
                                AnswerType: 1,
                                IsRequired: true,
                                Options:
                                [
                                    new SaveFormQuestionOptionRequest("Y", "Yes"),
                                    new SaveFormQuestionOptionRequest("N", "No"),
                                ]),
                        ]),
                    new SaveFormSectionRequest(
                        "Symptoms",
                        [new SaveFormQuestionRequest("When did symptoms begin?")]),
                ]));

            Assert.Equal("Gastro supplement", created.Name);
            Assert.Equal(condition, created.ConditionId);
            Assert.Equal("Malaria", created.ConditionName);
            Assert.True(created.AutoAssign);

            Assert.Equal(2, created.Sections.Count);
            Assert.Equal("Exposure", created.Sections[0].Title);
            Assert.Equal("Symptoms", created.Sections[1].Title);

            var exposure = created.Sections[0];
            Assert.Equal(2, exposure.Questions.Count);
            Assert.Equal("Where did you eat?", exposure.Questions[0].Text);
            Assert.Empty(exposure.Questions[0].Options);

            var coded = exposure.Questions[1];
            Assert.Equal(1, coded.AnswerType);
            Assert.True(coded.IsRequired);
            Assert.Equal(["Y", "N"], coded.Options.Select(option => option.Code));
            Assert.Equal(["Yes", "No"], coded.Options.Select(option => option.Label));

            // Read it back through GET and see the same tree.
            using var get = await api.CreateClient().GetAsync($"/api/forms/{created.Id}");
            get.EnsureSuccessStatusCode();
            var read = (await get.Content.ReadFromJsonAsync<FormDetailResponse>(Json))!;
            Assert.Equal(2, read.Sections.Count);
            Assert.Equal(
                "Did you eat shellfish?",
                read.Sections[0].Questions[1].Text);
            Assert.Equal(2, read.Sections[0].Questions[1].Options.Count);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ASaveReplacesTheWholeTree()
    {
        await Clear();

        try
        {
            var condition = await ConditionId("Malaria");

            var created = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Where did you eat?")]),
                ]));

            using var put = await api.CreateClient().PutAsJsonAsync(
                $"/api/forms/{created.Id}",
                new SaveFormRequest(
                    "Gastro supplement v2",
                    condition,
                    AutoAssign: true,
                    Sections:
                    [
                        new SaveFormSectionRequest(
                            "Symptoms",
                            [new SaveFormQuestionRequest("When did symptoms begin?")]),
                    ]),
                Json);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var saved = (await put.Content.ReadFromJsonAsync<FormDetailResponse>(Json))!;
            Assert.Equal("Gastro supplement v2", saved.Name);
            Assert.True(saved.AutoAssign);
            Assert.Equal("Symptoms", Assert.Single(saved.Sections).Title);
            Assert.Equal(
                "When did symptoms begin?",
                saved.Sections[0].Questions.Single().Text);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task TheListComesBackNewestFirstWithTheConditionNamed()
    {
        await Clear();

        try
        {
            var condition = await ConditionId("Malaria");

            var first = await Build(new SaveFormRequest("First", condition));
            var second = await Build(new SaveFormRequest("Second", condition, AutoAssign: true));

            using var response = await api.CreateClient().GetAsync("/api/forms?pageSize=200");
            response.EnsureSuccessStatusCode();
            var page = (await response.Content
                .ReadFromJsonAsync<PagedResult<FormResponse>>(Json))!;

            var ours = page.Items
                .Where(form => form.Id == first.Id || form.Id == second.Id)
                .ToList();
            Assert.Equal(2, ours.Count);
            Assert.Equal(second.Id, ours[0].Id);
            Assert.Equal(first.Id, ours[1].Id);
            Assert.Equal("Malaria", ours[0].ConditionName);
            Assert.True(ours[0].AutoAssign);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnUnknownFormIsAFourOhFour()
    {
        var condition = await ConditionId("Malaria");

        using var get = await api.CreateClient().GetAsync("/api/forms/987654321");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using var put = await api.CreateClient().PutAsJsonAsync(
            "/api/forms/987654321",
            new SaveFormRequest("Nowhere", condition),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task AFormWithNoNameIsRefused()
    {
        var condition = await ConditionId("Malaria");

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/forms",
            new SaveFormRequest("", condition),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownConditionIsRefused()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/forms",
            new SaveFormRequest("Bad condition", ConditionId: 987654321),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACodedQuestionWithNoOptionsIsRefused()
    {
        var condition = await ConditionId("Malaria");

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/forms",
            new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Pick one", AnswerType: 1)]),
                ]),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AFormIsAssignedToACaseAndListed()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Where did you eat?")]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");

            using var assign = await api.CreateClient()
                .PostAsync($"/api/cases/{caseId}/forms/{form.Id}", content: null);
            Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
            var assignment = (await assign.Content
                .ReadFromJsonAsync<FormAssignmentResponse>(Json))!;
            Assert.Equal(form.Id, assignment.FormId);
            Assert.Equal("Gastro supplement", assignment.FormName);

            // Assigning the same form again is the same row.
            using var again = await api.CreateClient()
                .PostAsync($"/api/cases/{caseId}/forms/{form.Id}", content: null);
            Assert.Equal(HttpStatusCode.Created, again.StatusCode);
            var reassigned = (await again.Content
                .ReadFromJsonAsync<FormAssignmentResponse>(Json))!;
            Assert.Equal(assignment.Id, reassigned.Id);

            using var list = await api.CreateClient().GetAsync($"/api/cases/{caseId}/forms");
            list.EnsureSuccessStatusCode();
            var assignments = (await list.Content
                .ReadFromJsonAsync<IReadOnlyList<FormAssignmentResponse>>(Json))!;
            Assert.Equal(assignment.Id, Assert.Single(assignments).Id);

            // The form opens with its tree and no answers yet.
            using var open = await api.CreateClient().GetAsync($"/api/cases/forms/{assignment.Id}");
            open.EnsureSuccessStatusCode();
            var caseForm = (await open.Content.ReadFromJsonAsync<CaseFormResponse>(Json))!;
            var question = caseForm.Sections[0].Questions[0];
            Assert.Null(question.TextAnswer);
            Assert.Null(question.Code);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task AnswersAreSavedAndReadBackWhole()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [
                            new SaveFormQuestionRequest("Where did you eat?"),
                            new SaveFormQuestionRequest(
                                "Did you eat shellfish?",
                                AnswerType: 1,
                                Options:
                                [
                                    new SaveFormQuestionOptionRequest("Y", "Yes"),
                                    new SaveFormQuestionOptionRequest("N", "No"),
                                ]),
                        ]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var assignment = await AssignForm(caseId, form.Id);
            var opened = await OpenCaseForm(assignment.Id);

            var textQuestion = opened.Sections[0].Questions[0];
            var codedQuestion = opened.Sections[0].Questions[1];

            using var save = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/forms/{assignment.Id}/answers",
                new SaveFormAnswersRequest(
                [
                    new FormAnswerInput(textQuestion.Id, TextAnswer: "At the diner"),
                    new FormAnswerInput(codedQuestion.Id, Code: "Y"),
                ]),
                Json);
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);

            var saved = (await save.Content.ReadFromJsonAsync<CaseFormResponse>(Json))!;
            Assert.Equal(
                "At the diner",
                saved.Sections[0].Questions[0].TextAnswer);
            Assert.Equal("Y", saved.Sections[0].Questions[1].Code);

            // Read back through GET and see the same answers.
            var reread = await OpenCaseForm(assignment.Id);
            Assert.Equal("At the diner", reread.Sections[0].Questions[0].TextAnswer);
            Assert.Equal("Y", reread.Sections[0].Questions[1].Code);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task ARequiredQuestionLeftBlankIsRefused()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Where did you eat?", IsRequired: true)]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var assignment = await AssignForm(caseId, form.Id);

            using var save = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/forms/{assignment.Id}/answers",
                new SaveFormAnswersRequest([]),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task AnAnswerWithACodeThatIsNotAnOptionIsRefused()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [
                            new SaveFormQuestionRequest(
                                "Did you eat shellfish?",
                                AnswerType: 1,
                                Options:
                                [
                                    new SaveFormQuestionOptionRequest("Y", "Yes"),
                                    new SaveFormQuestionOptionRequest("N", "No"),
                                ]),
                        ]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var assignment = await AssignForm(caseId, form.Id);
            var opened = await OpenCaseForm(assignment.Id);
            var coded = opened.Sections[0].Questions[0];

            using var save = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/forms/{assignment.Id}/answers",
                new SaveFormAnswersRequest([new FormAnswerInput(coded.Id, Code: "Z")]),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task AnUnknownCaseFormAndAssignmentAreRefused()
    {
        using var unknownCaseList = await api.CreateClient().GetAsync("/api/cases/987654321/forms");
        Assert.Equal(HttpStatusCode.NotFound, unknownCaseList.StatusCode);

        using var unknownAssignment = await api.CreateClient().GetAsync("/api/cases/forms/987654321");
        Assert.Equal(HttpStatusCode.NotFound, unknownAssignment.StatusCode);

        using var save = await api.CreateClient().PutAsJsonAsync(
            "/api/cases/forms/987654321/answers",
            new SaveFormAnswersRequest([]),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
    }

    /// <summary>Assigns a form to a case and answers its assignment.</summary>
    private async Task<FormAssignmentResponse> AssignForm(int personConditionId, int formId)
    {
        using var response = await api.CreateClient()
            .PostAsync($"/api/cases/{personConditionId}/forms/{formId}", content: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FormAssignmentResponse>(Json))!;
    }

    /// <summary>Opens an assigned form for completion.</summary>
    private async Task<CaseFormResponse> OpenCaseForm(int assignmentId)
    {
        using var response = await api.CreateClient().GetAsync($"/api/cases/forms/{assignmentId}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CaseFormResponse>(Json))!;
    }

    private async Task<int> PersonId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/persons?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<PersonResponse>>(Json))!;

        return page.Items
            .Single(person => $"{person.LastName}, {person.FirstName}" == named)
            .Id;
    }

    /// <summary>Opens a case for a person and condition, and answers its id.</summary>
    private async Task<int> CreateCase(string personNamed, string conditionNamed)
    {
        var person = await PersonId(personNamed);
        var condition = await ConditionId(conditionNamed);

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(person, condition),
            Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.Id;
    }

    /// <summary>
    /// Deletes the case, which cascades to its form assignments and their
    /// answers, so a test leaves no case behind.
    /// </summary>
    private async Task DeleteCase(int personConditionId)
    {
        if (personConditionId == 0)
        {
            return;
        }

        await using var context = server.NewContext();
        await context.PersonConditions
            .Where(report => report.Id == personConditionId)
            .ExecuteDeleteAsync();
    }

    [Fact]
    public async Task EditingAFormWhoseQuestionsAreAnsweredIsRefusedAndLeavesItUnchanged()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Where did you eat?")]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var assignment = await AssignForm(caseId, form.Id);
            var opened = await OpenCaseForm(assignment.Id);
            var question = opened.Sections[0].Questions[0];

            using var save = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/forms/{assignment.Id}/answers",
                new SaveFormAnswersRequest([new FormAnswerInput(question.Id, TextAnswer: "At the diner")]),
                Json);
            save.EnsureSuccessStatusCode();

            // Restructuring the answered form is refused, and nothing changes.
            using var edit = await api.CreateClient().PutAsJsonAsync(
                $"/api/forms/{form.Id}",
                new SaveFormRequest(
                    "Renamed",
                    condition,
                    Sections:
                    [
                        new SaveFormSectionRequest(
                            "Symptoms",
                            [new SaveFormQuestionRequest("When did symptoms begin?")]),
                    ]),
                Json);
            Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

            using var get = await api.CreateClient().GetAsync($"/api/forms/{form.Id}");
            get.EnsureSuccessStatusCode();
            var unchanged = (await get.Content.ReadFromJsonAsync<FormDetailResponse>(Json))!;
            Assert.Equal("Gastro supplement", unchanged.Name);
            Assert.Equal("Exposure", Assert.Single(unchanged.Sections).Title);
            Assert.Equal(
                "Where did you eat?",
                unchanged.Sections[0].Questions.Single().Text);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task AnAnswerForAQuestionNotOnTheFormIsRefused()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Where did you eat?")]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var assignment = await AssignForm(caseId, form.Id);

            using var save = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/forms/{assignment.Id}/answers",
                new SaveFormAnswersRequest([new FormAnswerInput(987654321, TextAnswer: "x")]),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task DuplicateAnswerInputsForOneQuestionTakeTheLastValue()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var condition = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest(
                "Gastro supplement",
                condition,
                Sections:
                [
                    new SaveFormSectionRequest(
                        "Exposure",
                        [new SaveFormQuestionRequest("Where did you eat?")]),
                ]));

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var assignment = await AssignForm(caseId, form.Id);
            var opened = await OpenCaseForm(assignment.Id);
            var questionId = opened.Sections[0].Questions[0].Id;

            using var save = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/forms/{assignment.Id}/answers",
                new SaveFormAnswersRequest(
                [
                    new FormAnswerInput(questionId, TextAnswer: "first"),
                    new FormAnswerInput(questionId, TextAnswer: "second"),
                ]),
                Json);
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);

            var saved = (await save.Content.ReadFromJsonAsync<CaseFormResponse>(Json))!;
            Assert.Equal("second", saved.Sections[0].Questions[0].TextAnswer);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task AFormCannotBeAssignedToACaseOfAnotherCondition()
    {
        await Clear();
        int caseId = 0;

        try
        {
            var malaria = await ConditionId("Malaria");
            var form = await Build(new SaveFormRequest("Gastro supplement", malaria));

            caseId = await CreateCase("Rodriguez, Maria", "Giardiasis");

            using var assign = await api.CreateClient()
                .PostAsync($"/api/cases/{caseId}/forms/{form.Id}", content: null);
            Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    /// <summary>Builds a form and answers its detail.</summary>
    private async Task<FormDetailResponse> Build(SaveFormRequest request)
    {
        using var response = await api.CreateClient().PostAsJsonAsync("/api/forms", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FormDetailResponse>(Json))!;
    }

    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }

    /// <summary>
    /// Takes every form assignment and then every form out of the tables, so
    /// each test starts and ends with no forms; the assignments go first
    /// because a form a case holds cannot be deleted out from under it, and
    /// deleting a form cascades to its sections, questions and options.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.FormAssignments.ExecuteDeleteAsync();
        await context.Forms.ExecuteDeleteAsync();
    }
}
