using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/forms</c>: the condition-specific supplemental forms a public health
/// agency builds — a form of nested sections and questions, each question
/// answered as free text or a coded choice.
/// </summary>
/// <remarks>
/// The forms are the <c>forms</c> table and their tree the <c>form_sections</c>,
/// <c>form_questions</c> and <c>form_question_options</c> tables, as this
/// session's migration created them. The handlers are handed
/// <see cref="FormQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature, as <see cref="OutbreakEndpoints"/> is handed
/// <see cref="OutbreakQueries"/>.
///
/// A form is saved whole: <c>POST</c> and <c>PUT</c> take the form with its
/// sections, questions and options in one <see cref="SaveFormRequest"/>, and a
/// <c>PUT</c> replaces the tree rather than patching it. Whether the condition
/// a form names exists is a question for the database, which the endpoint asks
/// and refuses in the words <see cref="NotACondition"/> gives — the same
/// sentence <see cref="OutbreakEndpoints"/> uses, so a caller reads one phrasing
/// whichever slice refused it.
/// </remarks>
public static class FormEndpoints
{
    private const int NameLength = 200;

    private const int TitleLength = 200;

    private const int CodeLength = 50;

    private const int LabelLength = 200;

    /// <summary>
    /// The longest a text answer may be. The column is unbounded, so this is a
    /// guard against an upload that is an accident rather than a column
    /// constraint, as <see cref="OutbreakEndpoints.ReportTextLength"/> is: a
    /// hundred thousand characters is longer than any answer somebody types and
    /// shorter than a file somebody sent by mistake.
    /// </summary>
    public const int AnswerTextLength = 100_000;

    /// <summary>A question answered as free text.</summary>
    public const int AnswerTypeText = 0;

    /// <summary>A question answered as one of a coded set of choices.</summary>
    public const int AnswerTypeCoded = 1;

    /// <summary>
    /// The projection from the stored form to the list row a screen reads, as
    /// the form's own columns. Public because the unit suite proves it.
    /// </summary>
    /// <remarks>
    /// The condition's name is not the form's own, so it is null here; the list
    /// query joins the name on, since a <see cref="FormResponse"/> on the wire
    /// carries it. This projection is what the form is in itself.
    /// </remarks>
    public static readonly Expression<Func<Form, FormResponse>> ToResponse =
        form => new FormResponse(
            form.Id,
            form.Name,
            form.ConditionId,
            null,
            form.AutoAssign,
            form.CreatedAt);

    /// <summary>
    /// Why this is not a form this tier will save, or null when it is one.
    /// </summary>
    /// <remarks>
    /// Pure, and about the shape alone: the name is required and bounded, each
    /// section has a title, each question has text and a known answer type, a
    /// coded question has at least one option and each option a code and a
    /// label, and a text question has no options. Whether the condition a
    /// request names exists is a question for the database, so the endpoint
    /// asks <see cref="FormQueries.ConditionExists"/> that, exactly as
    /// <see cref="OutbreakEndpoints"/> leaves a reference's existence to a
    /// query — a pure validator cannot know what is on file.
    /// </remarks>
    public static string? WhyNotAForm(SaveFormRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: a form is chosen from a list by its name.";
        }

        if (request.Name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        foreach (var section in request.Sections ?? [])
        {
            if (string.IsNullOrWhiteSpace(section.Title))
            {
                return "a section has no title: every section is shown under a heading.";
            }

            if (section.Title.Trim().Length > TitleLength)
            {
                return $"a section title is longer than {TitleLength} characters.";
            }

            foreach (var question in section.Questions ?? [])
            {
                if (WhyNotAQuestion(question) is { } why)
                {
                    return why;
                }
            }
        }

        return null;
    }

    private static string? WhyNotAQuestion(SaveFormQuestionRequest question)
    {
        if (string.IsNullOrWhiteSpace(question.Text))
        {
            return "a question has no text: every question is shown by its prompt.";
        }

        if (question.AnswerType is not (AnswerTypeText or AnswerTypeCoded))
        {
            return $"answerType {question.AnswerType} is not a known answer type. A question is "
                + $"free text ({AnswerTypeText}) or coded ({AnswerTypeCoded}).";
        }

        var options = question.Options ?? [];

        if (question.AnswerType == AnswerTypeText)
        {
            return options.Count > 0
                ? "a text question carries options: only a coded question has a set of choices."
                : null;
        }

        if (options.Count == 0)
        {
            return "a coded question carries no options: a coded question is answered from a "
                + "set of choices.";
        }

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Code))
            {
                return "an option has no code: a choice stores its code when it is picked.";
            }

            if (option.Code.Trim().Length > CodeLength)
            {
                return $"an option code is longer than {CodeLength} characters.";
            }

            if (string.IsNullOrWhiteSpace(option.Label))
            {
                return "an option has no label: a choice is shown by its label.";
            }

            if (option.Label.Trim().Length > LabelLength)
            {
                return $"an option label is longer than {LabelLength} characters.";
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence a condition no form can be for is refused with, whether it
    /// is one nothing can have or one no condition on file has.
    /// </summary>
    public static string NotACondition(int conditionId) =>
        $"conditionId {conditionId} is not a condition this tier knows. The conditions are the "
            + "ones GET /api/conditions lists.";

    /// <summary>
    /// The sentence a form assigned to a case of another condition is refused
    /// with: a form is for one condition and is assigned only to a case of it.
    /// </summary>
    public static string CrossCondition(int formId) =>
        $"form {formId} is for a different condition than this case. A supplemental form is "
            + "assigned only to a case of its condition.";

    /// <summary>Registers what the form endpoints need of the container.</summary>
    public static IServiceCollection AddForms(this IServiceCollection services) =>
        services.AddScoped<FormQueries>();

    public static IEndpointRouteBuilder MapForms(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/forms", GetForms)
            .WithName("GetForms")
            .WithSummary("The supplemental forms, paged and newest first.");

        routes.MapGet("/api/forms/{id:int}", GetForm)
            .WithName("GetForm")
            .WithSummary("One form, with its sections, questions and coded options in order.");

        routes.MapPost("/api/forms", CreateForm)
            .WithName("CreateForm")
            .WithSummary("Builds a form from its whole section/question/option tree.");

        routes.MapPut("/api/forms/{id:int}", UpdateForm)
            .WithName("UpdateForm")
            .WithSummary("Saves over a form, replacing its whole tree.");

        routes.MapGet("/api/cases/{personConditionId:int}/forms", GetCaseForms)
            .WithName("GetCaseForms")
            .WithSummary("The forms assigned to a case, to complete.");

        routes.MapPost("/api/cases/{personConditionId:int}/forms/{formId:int}", AssignForm)
            .WithName("AssignForm")
            .WithSummary("Assigns a form to a case.");

        routes.MapGet("/api/cases/forms/{assignmentId:int}", GetCaseForm)
            .WithName("GetCaseForm")
            .WithSummary("One assigned form, its tree and the case's answers so far.");

        routes.MapPut("/api/cases/forms/{assignmentId:int}/answers", SaveCaseFormAnswers)
            .WithName("SaveCaseFormAnswers")
            .WithSummary("Saves a case's answers to an assigned form.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<FormResponse>>, ProblemHttpResult>> GetForms(
        FormQueries forms,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        return TypedResults.Ok(await forms.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<FormDetailResponse>, ProblemHttpResult>> GetForm(
        FormQueries forms,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await forms.Detail(id, cancellationToken) is not { } form)
        {
            return NoSuchForm(id);
        }

        return TypedResults.Ok(form);
    }

    private static async Task<Results<Created<FormDetailResponse>, ProblemHttpResult>> CreateForm(
        FormQueries forms,
        SaveFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAForm(request) is { } why)
        {
            return Refused(why);
        }

        if (!await forms.ConditionExists(request.ConditionId, cancellationToken))
        {
            return Refused(NotACondition(request.ConditionId));
        }

        var created = await forms.Add(request, cancellationToken);

        return TypedResults.Created($"/api/forms/{created.Id}", created);
    }

    private static async Task<Results<Ok<FormDetailResponse>, ProblemHttpResult>> UpdateForm(
        FormQueries forms,
        int id,
        SaveFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAForm(request) is { } why)
        {
            return Refused(why);
        }

        if (!await forms.ConditionExists(request.ConditionId, cancellationToken))
        {
            return Refused(NotACondition(request.ConditionId));
        }

        // A form whose questions a case has answered cannot be restructured —
        // the answers reference the questions and would be orphaned — so the
        // edit is refused whole rather than allowed to fail part-way.
        if (await forms.HasAnswers(id, cancellationToken))
        {
            return FormHasAnswers(id);
        }

        if (await forms.Update(id, request, cancellationToken) is not { } form)
        {
            return NoSuchForm(id);
        }

        return TypedResults.Ok(form);
    }

    private static async Task<Results<Ok<IReadOnlyList<FormAssignmentResponse>>, ProblemHttpResult>>
        GetCaseForms(
            FormQueries forms,
            int personConditionId,
            CancellationToken cancellationToken = default)
    {
        if (await forms.Assignments(personConditionId, cancellationToken) is not { } assignments)
        {
            return NoSuchCase(personConditionId);
        }

        return TypedResults.Ok(assignments);
    }

    private static async Task<Results<Created<FormAssignmentResponse>, ProblemHttpResult>> AssignForm(
        FormQueries forms,
        int personConditionId,
        int formId,
        CancellationToken cancellationToken = default)
    {
        if (!await forms.CaseExists(personConditionId, cancellationToken))
        {
            return NoSuchCase(personConditionId);
        }

        if (!await forms.FormExists(formId, cancellationToken))
        {
            return NoSuchForm(formId);
        }

        // A form is condition-specific, so it is assigned only to a case of its
        // condition, by hand as by the auto-assign rule.
        if (!await forms.FormMatchesCaseCondition(personConditionId, formId, cancellationToken))
        {
            return RefusedAssignment(CrossCondition(formId));
        }

        var assignment = await forms.Assign(personConditionId, formId, cancellationToken);

        return TypedResults.Created($"/api/cases/forms/{assignment.Id}", assignment);
    }

    private static async Task<Results<Ok<CaseFormResponse>, ProblemHttpResult>> GetCaseForm(
        FormQueries forms,
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        if (await forms.CaseForm(assignmentId, cancellationToken) is not { } form)
        {
            return NoSuchAssignment(assignmentId);
        }

        return TypedResults.Ok(form);
    }

    private static async Task<Results<Ok<CaseFormResponse>, ProblemHttpResult>> SaveCaseFormAnswers(
        FormQueries forms,
        int assignmentId,
        SaveFormAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await forms.AnswerableQuestions(assignmentId, cancellationToken) is not { } questions)
        {
            return NoSuchAssignment(assignmentId);
        }

        if (WhyNotTheseAnswers(questions, request) is { } why)
        {
            return RefusedAnswers(why);
        }

        var form = await forms.SaveAnswers(assignmentId, request, cancellationToken);

        return TypedResults.Ok(form!);
    }

    /// <summary>
    /// Why the answers given are not ones this tier will save, or null when they
    /// are. Pure, over the form's questions as they are on file: a required
    /// question must have a non-blank answer, a coded answer's code must be one
    /// of the question's options, and a text answer may not run past its bound.
    /// </summary>
    private static string? WhyNotTheseAnswers(
        IReadOnlyList<FormQuestionResponse> questions,
        SaveFormAnswersRequest request)
    {
        var known = questions.Select(question => question.Id).ToHashSet();

        foreach (var answer in request.Answers ?? [])
        {
            if (!known.Contains(answer.QuestionId))
            {
                return $"question {answer.QuestionId} is not a question of this form.";
            }
        }

        var given = (request.Answers ?? [])
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (var question in questions)
        {
            given.TryGetValue(question.Id, out var answer);
            var text = answer?.TextAnswer?.Trim();
            var code = answer?.Code?.Trim();

            if (question.AnswerType == AnswerTypeText)
            {
                if (text is { Length: > AnswerTextLength })
                {
                    return $"a text answer is longer than {AnswerTextLength} characters.";
                }

                if (question.IsRequired && string.IsNullOrWhiteSpace(text))
                {
                    return $"question {question.Id} is required and is unanswered.";
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(code)
                    && !question.Options.Any(option => option.Code == code))
                {
                    return $"code {code} is not a choice for question {question.Id}.";
                }

                if (question.IsRequired && string.IsNullOrWhiteSpace(code))
                {
                    return $"question {question.Id} is required and is unanswered.";
                }
            }
        }

        return null;
    }

    private static ProblemHttpResult NoSuchForm(int id) =>
        TypedResults.Problem(
            detail: $"No form has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such form.");

    private static ProblemHttpResult NoSuchCase(int id) =>
        TypedResults.Problem(
            detail: $"No case has person-condition id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case.");

    private static ProblemHttpResult NoSuchAssignment(int id) =>
        TypedResults.Problem(
            detail: $"No form assignment has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such form assignment.");

    private static ProblemHttpResult FormHasAnswers(int id) =>
        TypedResults.Problem(
            detail: $"Form {id} has answers recorded against it and its sections and questions "
                + "cannot be restructured. Build a new form instead.",
            statusCode: StatusCodes.Status409Conflict,
            title: "The form is in use and cannot be restructured.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The form asked for is not one this endpoint can save.");

    private static ProblemHttpResult RefusedAnswers(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The answers given are not ones this endpoint can save.");

    private static ProblemHttpResult RefusedAssignment(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The form asked for is not one this case can be assigned.");
}

/// <summary>
/// The form queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for forms and their tree.
/// </summary>
/// <remarks>
/// Internal, as every slice's queries are. It reads the conditions directly
/// rather than through their slice's queries: it holds the one
/// <c>DbContext</c>, and a cross-slice call would buy nothing but a second hop
/// to the same table.
/// </remarks>
internal sealed class FormQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of forms, newest first and then by id — by id as well so two
    /// forms created in the same instant keep a total order. The condition's
    /// name is joined on for the row.
    /// </summary>
    public async Task<PagedResult<FormResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Forms.CountAsync(cancellationToken);

        var rows = await database.Forms
            .OrderByDescending(form => form.CreatedAt)
            .ThenByDescending(form => form.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(form => new FormResponse(
                form.Id,
                form.Name,
                form.ConditionId,
                database.Conditions
                    .Where(condition => condition.Id == form.ConditionId)
                    .Select(condition => condition.Name)
                    .FirstOrDefault(),
                form.AutoAssign,
                form.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<FormResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// The form with that id, its condition named, and its whole tree in order;
    /// null where no form has the id.
    /// </summary>
    public async Task<FormDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        var form = await database.Forms
            .SingleOrDefaultAsync(form => form.Id == id, cancellationToken);

        if (form is null)
        {
            return null;
        }

        var conditionName = await database.Conditions
            .Where(condition => condition.Id == form.ConditionId)
            .Select(condition => condition.Name)
            .SingleOrDefaultAsync(cancellationToken);

        var (sections, questions, options) = await LoadTree(id, cancellationToken);

        var sectionResponses = sections
            .Select(section => new FormSectionResponse(
                section.Id,
                section.Title,
                section.SortOrder,
                questions
                    .Where(question => question.FormSectionId == section.Id)
                    .Select(question => new FormQuestionResponse(
                        question.Id,
                        question.Text,
                        question.HelpText,
                        question.AnswerType,
                        question.IsRequired,
                        question.SortOrder,
                        options
                            .Where(option => option.FormQuestionId == question.Id)
                            .Select(option => new FormQuestionOptionResponse(
                                option.Id,
                                option.Code,
                                option.Label,
                                option.SortOrder))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new FormDetailResponse(
            form.Id,
            form.Name,
            form.Description,
            form.ConditionId,
            conditionName,
            form.AutoAssign,
            form.CreatedAt,
            sectionResponses);
    }

    /// <summary>Creates the form and its tree, and answers its detail.</summary>
    public async Task<FormDetailResponse> Add(SaveFormRequest request, CancellationToken cancellationToken)
    {
        var form = new Form
        {
            Name = request.Name.Trim(),
            ConditionId = request.ConditionId,
            AutoAssign = request.AutoAssign,
            Description = Text(request.Description),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        database.Forms.Add(form);
        await database.SaveChangesAsync(cancellationToken);

        await WriteTree(form.Id, request, cancellationToken);

        return (await Detail(form.Id, cancellationToken))!;
    }

    /// <summary>
    /// Saves over the form and replaces its whole tree, or answers null where
    /// no form has that id.
    /// </summary>
    public async Task<FormDetailResponse?> Update(
        int id,
        SaveFormRequest request,
        CancellationToken cancellationToken)
    {
        var form = await database.Forms
            .SingleOrDefaultAsync(form => form.Id == id, cancellationToken);

        if (form is null)
        {
            return null;
        }

        // The scalar save, the tree delete and the rebuild are one transaction,
        // so a form is saved whole or not at all — never left with new scalar
        // fields over an old or half-deleted tree. The caller has proven no
        // case has answered the form, so the restrictive cascade from an answer
        // to its question cannot refuse the delete here.
        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);

        form.Name = request.Name.Trim();
        form.ConditionId = request.ConditionId;
        form.AutoAssign = request.AutoAssign;
        form.Description = Text(request.Description);
        await database.SaveChangesAsync(cancellationToken);

        // Replace the tree: deleting the sections cascades to their questions
        // and those questions' options, so the whole tree goes and is rebuilt
        // from the request.
        await database.FormSections
            .Where(section => section.FormId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await WriteTree(id, request, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await Detail(id, cancellationToken);
    }

    /// <summary>Whether a condition this tier knows has that id.</summary>
    public Task<bool> ConditionExists(int id, CancellationToken cancellationToken) =>
        database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken);

    /// <summary>Whether a form has that id.</summary>
    public Task<bool> FormExists(int id, CancellationToken cancellationToken) =>
        database.Forms.AnyAsync(form => form.Id == id, cancellationToken);

    /// <summary>Whether a case (a person_condition) has that id.</summary>
    public Task<bool> CaseExists(int personConditionId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == personConditionId, cancellationToken);

    /// <summary>
    /// Whether any case has answered a question of the form — the test the
    /// update path reads to refuse restructuring a form that is in use.
    /// </summary>
    public Task<bool> HasAnswers(int formId, CancellationToken cancellationToken) =>
        (from answer in database.FormAnswers
         join question in database.FormQuestions on answer.FormQuestionId equals question.Id
         join section in database.FormSections on question.FormSectionId equals section.Id
         where section.FormId == formId
         select answer.Id)
        .AnyAsync(cancellationToken);

    /// <summary>
    /// Whether the form is for the case's condition — the test the manual
    /// assignment path reads to keep a form to a case of its condition. Both
    /// the case and the form are known to exist.
    /// </summary>
    public async Task<bool> FormMatchesCaseCondition(
        int personConditionId,
        int formId,
        CancellationToken cancellationToken)
    {
        var caseCondition = await database.PersonConditions
            .Where(report => report.Id == personConditionId)
            .Select(report => report.ConditionId)
            .SingleAsync(cancellationToken);

        var formCondition = await database.Forms
            .Where(form => form.Id == formId)
            .Select(form => form.ConditionId)
            .SingleAsync(cancellationToken);

        return caseCondition == formCondition;
    }

    /// <summary>
    /// The forms assigned to the case, oldest assignment first; null where no
    /// case has the id.
    /// </summary>
    public async Task<IReadOnlyList<FormAssignmentResponse>?> Assignments(
        int personConditionId,
        CancellationToken cancellationToken)
    {
        if (!await CaseExists(personConditionId, cancellationToken))
        {
            return null;
        }

        return await database.FormAssignments
            .Where(assignment => assignment.PersonConditionId == personConditionId)
            .Join(
                database.Forms,
                assignment => assignment.FormId,
                form => form.Id,
                (assignment, form) => new FormAssignmentResponse(
                    assignment.Id,
                    form.Id,
                    form.Name,
                    assignment.AssignedAt))
            .OrderBy(assignment => assignment.AssignedAt)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Assigns the form to the case, upserting the one row so a second
    /// assignment is the same row, and answers it. The caller has proven the
    /// case and the form exist.
    /// </summary>
    public async Task<FormAssignmentResponse> Assign(
        int personConditionId,
        int formId,
        CancellationToken cancellationToken)
    {
        var assignment = await database.FormAssignments.SingleOrDefaultAsync(
            assignment => assignment.FormId == formId
                && assignment.PersonConditionId == personConditionId,
            cancellationToken);

        if (assignment is null)
        {
            assignment = new FormAssignment
            {
                FormId = formId,
                PersonConditionId = personConditionId,
                AssignedAt = DateTimeOffset.UtcNow,
            };
            database.FormAssignments.Add(assignment);
            await database.SaveChangesAsync(cancellationToken);
        }

        var formName = await database.Forms
            .Where(form => form.Id == formId)
            .Select(form => form.Name)
            .SingleAsync(cancellationToken);

        return new FormAssignmentResponse(assignment.Id, formId, formName, assignment.AssignedAt);
    }

    /// <summary>
    /// The assigned form as the completion screen reads it — the form's tree
    /// with the case's answer against each question; null where no assignment
    /// has the id.
    /// </summary>
    public async Task<CaseFormResponse?> CaseForm(int assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await database.FormAssignments
            .SingleOrDefaultAsync(assignment => assignment.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var form = await database.Forms.SingleAsync(form => form.Id == assignment.FormId, cancellationToken);
        var (sections, questions, options) = await LoadTree(form.Id, cancellationToken);
        var answers = await database.FormAnswers
            .Where(answer => answer.FormAssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        var sectionResponses = sections
            .Select(section => new CaseFormSectionResponse(
                section.Id,
                section.Title,
                section.SortOrder,
                questions
                    .Where(question => question.FormSectionId == section.Id)
                    .Select(question =>
                    {
                        var answer = answers.FirstOrDefault(
                            answer => answer.FormQuestionId == question.Id);

                        return new CaseFormQuestionResponse(
                            question.Id,
                            question.Text,
                            question.HelpText,
                            question.AnswerType,
                            question.IsRequired,
                            question.SortOrder,
                            options
                                .Where(option => option.FormQuestionId == question.Id)
                                .Select(option => new FormQuestionOptionResponse(
                                    option.Id,
                                    option.Code,
                                    option.Label,
                                    option.SortOrder))
                                .ToList(),
                            answer?.TextAnswer,
                            answer?.Code);
                    })
                    .ToList()))
            .ToList();

        return new CaseFormResponse(
            assignment.Id,
            form.Id,
            form.Name,
            form.Description,
            sectionResponses);
    }

    /// <summary>
    /// The assignment's form's questions, each with its options, for validating
    /// a set of answers against; null where no assignment has the id.
    /// </summary>
    public async Task<IReadOnlyList<FormQuestionResponse>?> AnswerableQuestions(
        int assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await database.FormAssignments
            .SingleOrDefaultAsync(assignment => assignment.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var (_, questions, options) = await LoadTree(assignment.FormId, cancellationToken);

        return questions
            .Select(question => new FormQuestionResponse(
                question.Id,
                question.Text,
                question.HelpText,
                question.AnswerType,
                question.IsRequired,
                question.SortOrder,
                options
                    .Where(option => option.FormQuestionId == question.Id)
                    .Select(option => new FormQuestionOptionResponse(
                        option.Id,
                        option.Code,
                        option.Label,
                        option.SortOrder))
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Upserts the case's answers to the assignment's form — one row per
    /// question answered — and answers the whole form; null where no assignment
    /// has the id. The caller has proven the answers are valid.
    /// </summary>
    public async Task<CaseFormResponse?> SaveAnswers(
        int assignmentId,
        SaveFormAnswersRequest request,
        CancellationToken cancellationToken)
    {
        var assignment = await database.FormAssignments
            .SingleOrDefaultAsync(assignment => assignment.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var (_, questions, _) = await LoadTree(assignment.FormId, cancellationToken);
        var questionIds = questions.Select(question => question.Id).ToHashSet();

        var stored = await database.FormAnswers
            .Where(answer => answer.FormAssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        // One input per question — the last given wins — so a repeated question
        // is one upsert, not a second insert the unique index would refuse.
        var inputs = (request.Answers ?? [])
            .GroupBy(answer => answer.QuestionId)
            .Select(group => group.Last());

        foreach (var input in inputs)
        {
            if (!questionIds.Contains(input.QuestionId))
            {
                continue;
            }

            var text = Text(input.TextAnswer);
            var code = Text(input.Code);
            var answer = stored.FirstOrDefault(answer => answer.FormQuestionId == input.QuestionId);

            if (answer is null)
            {
                database.FormAnswers.Add(new FormAnswer
                {
                    FormAssignmentId = assignmentId,
                    FormQuestionId = input.QuestionId,
                    TextAnswer = text,
                    Code = code,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                answer.TextAnswer = text;
                answer.Code = code;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        return await CaseForm(assignmentId, cancellationToken);
    }

    /// <summary>
    /// The form's sections, their questions and those questions' options, each
    /// level in its sort order then by id.
    /// </summary>
    private async Task<(List<FormSection> Sections, List<FormQuestion> Questions, List<FormQuestionOption> Options)>
        LoadTree(int formId, CancellationToken cancellationToken)
    {
        var sections = await database.FormSections
            .Where(section => section.FormId == formId)
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Id)
            .ToListAsync(cancellationToken);

        var sectionIds = sections.Select(section => section.Id).ToList();

        var questions = await database.FormQuestions
            .Where(question => sectionIds.Contains(question.FormSectionId))
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);

        var questionIds = questions.Select(question => question.Id).ToList();

        var options = await database.FormQuestionOptions
            .Where(option => questionIds.Contains(option.FormQuestionId))
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Id)
            .ToListAsync(cancellationToken);

        return (sections, questions, options);
    }

    /// <summary>
    /// Writes the form's sections, their questions and each coded question's
    /// options for the form, numbering each level by the order of its list. The
    /// form already exists and holds no tree.
    /// </summary>
    private async Task WriteTree(int formId, SaveFormRequest request, CancellationToken cancellationToken)
    {
        var sections = (request.Sections ?? [])
            .Select((section, order) => (
                entity: new FormSection
                {
                    FormId = formId,
                    Title = section.Title.Trim(),
                    SortOrder = order,
                },
                request: section))
            .ToList();

        if (sections.Count == 0)
        {
            return;
        }

        database.FormSections.AddRange(sections.Select(section => section.entity));
        await database.SaveChangesAsync(cancellationToken);

        var questions = new List<(FormQuestion Entity, SaveFormQuestionRequest Request)>();
        foreach (var (section, sectionRequest) in sections)
        {
            foreach (var (question, order) in (sectionRequest.Questions ?? [])
                .Select((question, order) => (question, order)))
            {
                questions.Add((
                    new FormQuestion
                    {
                        FormSectionId = section.Id,
                        Text = question.Text.Trim(),
                        HelpText = Text(question.HelpText),
                        AnswerType = question.AnswerType,
                        IsRequired = question.IsRequired,
                        SortOrder = order,
                    },
                    question));
            }
        }

        if (questions.Count == 0)
        {
            return;
        }

        database.FormQuestions.AddRange(questions.Select(question => question.Entity));
        await database.SaveChangesAsync(cancellationToken);

        var options = new List<FormQuestionOption>();
        foreach (var (question, questionRequest) in questions)
        {
            if (question.AnswerType != FormEndpoints.AnswerTypeCoded)
            {
                continue;
            }

            foreach (var (option, order) in (questionRequest.Options ?? [])
                .Select((option, order) => (option, order)))
            {
                options.Add(new FormQuestionOption
                {
                    FormQuestionId = question.Id,
                    Code = option.Code.Trim(),
                    Label = option.Label.Trim(),
                    SortOrder = order,
                });
            }
        }

        if (options.Count == 0)
        {
            return;
        }

        database.FormQuestionOptions.AddRange(options);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
