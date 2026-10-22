using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using Survsys.Contracts;
using Survsys.Web;
using Survsys.Web.Components;

// The application tier: MudBlazor screens over what the API tier returned.
// It takes an API address and never a connection string -- it cannot reach
// the database, and ArchitectureTests holds it to that.
const string apiBaseAddressKey = "Survsys:ApiBaseAddress";

// Where a person who is not signed in is sent, and where the form there posts:
// a path of its own, because a page's route answers posts of its own as well.
const string signInPage = "/sign-in";
const string signInPost = "/account/sign-in";

// The folder this tier's data protection keys are kept in, where it is set.
const string dataProtectionKeysPathKey = "Survsys:DataProtectionKeysPath";

var builder = WebApplication.CreateBuilder(args);

var apiBaseAddress = builder.Configuration[apiBaseAddressKey];

if (string.IsNullOrWhiteSpace(apiBaseAddress))
{
    throw new InvalidOperationException(
        $"No API address is configured at '{apiBaseAddressKey}'. This tier shows only what "
        + "the API tier returns and cannot start without knowing where that tier is: set "
        + $"{apiBaseAddressKey.Replace(':', '_')} for this environment, or supply it in "
        + "appsettings.");
}

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// The keys that protect the sign-in cookie and the antiforgery tokens, in the
// folder configuration names, so a sign-in outlives an app-pool recycle and is
// read by every server of this tier. The application name is fixed because
// the default is the content root, which differs from one server to the next.
// Unset, the keys are the framework's default and a recycle signs everyone out.
if (builder.Configuration[dataProtectionKeysPathKey] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("Survsys.Web");
}

// A person signs in once, here, and this tier's cookie then carries who they
// are, their privileges and the API tier's token for them. Every screen needs
// a signed-in person, by one fallback policy; what each person may do is the
// API tier's to decide, and it decides it from the token.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = signInPage;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization(options => options.FallbackPolicy =
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCircuitServicesAccessor();
builder.Services.AddTransient<ApiTokenHandler>();

// What a case's save classified, read off the answer of the clients whose
// saves classify and kept for the circuit's report page.
builder.Services.AddScoped<CaseClassifications>();
builder.Services.AddTransient<CaseClassificationHandler>();

// The trailing slash matters: a base address without one would drop the last
// path segment when a relative request is resolved against it, so an API
// hosted under a path prefix would be asked the wrong URL.
var api = new Uri(apiBaseAddress.TrimEnd('/') + '/');

// The one client that asks without a token, because it is what gets one.
builder.Services.AddHttpClient<AccountClient>(http => http.BaseAddress = api);

// One typed client per resource this tier reads or writes, each against the
// one API address and each sending the signed-in person's token. A screen
// takes a client and never an HttpClient: what a failure says is the
// client's to decide, and a screen's to show.
builder.Services.AddHttpClient<ConditionsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<PersonsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<AgenciesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<FacilitiesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<CoreElementsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<PersonConditionsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<ClinicalClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>()
    .AddHttpMessageHandler<CaseClassificationHandler>();
builder.Services.AddHttpClient<InvestigationClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>()
    .AddHttpMessageHandler<CaseClassificationHandler>();
builder.Services.AddHttpClient<AdministrativeClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>()
    .AddHttpMessageHandler<CaseClassificationHandler>();
builder.Services.AddHttpClient<LabsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>()
    .AddHttpMessageHandler<CaseClassificationHandler>();
builder.Services.AddHttpClient<ExposuresClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<NotesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<AttachmentsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<TasksClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<WorkQueueClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<CasesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<UsersClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<RolesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<SavedViewsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<SystemSettingsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<MessageQueueClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<EcrClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<NotificationQueueClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<ExportLogClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<AuditClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<ReconciliationCutoffClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<ReportsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<OutbreaksClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<FormsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<TreatmentsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<VaccinesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<EncountersClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>()
    .AddHttpMessageHandler<CaseClassificationHandler>();
builder.Services.AddHttpClient<CodesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<VocabularyClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<ValueSetsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<GuidesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<PhinVariablesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<RuleSetsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<HepLabGroupsClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();
builder.Services.AddHttpClient<PlacesClient>(http => http.BaseAddress = api).AddHttpMessageHandler<ApiTokenHandler>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Required by the component endpoints below, which refuse to be mapped
// without it, and by the sign-in and sign-out forms, which it protects.
app.UseAntiforgery();

// Stylesheets and scripts are served to anyone, so the sign-in page is styled
// for a person who has not signed in yet.
app.MapStaticAssets().AllowAnonymous();

// What the walkthrough harness waits on before its first click. A tier
// answers here only once startup has finished, so a 200 means this tier is
// serving -- which is the whole of what the harness needs to know and the
// whole of what this endpoint says.
app.MapGet("/health/ready", () => Results.Ok("ready")).AllowAnonymous();

// The sign-in form's post: the API tier is asked for a token and for who it
// says the person is, and the cookie is written from both. A refusal goes back
// to the form with what to tell the person, and the return address is followed
// only where it is local, so the form cannot be used to send anyone elsewhere.
app.MapPost(signInPost, async (
        [FromForm] string? userName,
        [FromForm] string? password,
        [FromForm] string? returnUrl,
        AccountClient accounts,
        HttpContext http,
        CancellationToken cancellationToken) =>
    {
        var token = await accounts.SignIn(new SignInRequest(userName ?? "", password ?? ""), cancellationToken);
        var user = token.Value is { } value ? await accounts.Me(value, cancellationToken) : null;

        if (token.Value is null || user?.Value is null)
        {
            // A word for the page to say rather than the message itself, so a
            // link to the page cannot put words of its own in front of a person.
            var failure = (token.Failure ?? user?.Failure) == AccountClient.RefusedFailure ? "refused" : "unavailable";

            return Results.Redirect(
                $"{signInPage}?failure={failure}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountClient.PrincipalFor(user.Value, token.Value));

        return Results.LocalRedirect(
            returnUrl is { Length: > 0 } local && Uri.IsWellFormedUriString(local, UriKind.Relative) && local.StartsWith('/')
                && !local.StartsWith("//", StringComparison.Ordinal)
                ? local
                : "/");
    })
    .AllowAnonymous();

app.MapPost("/account/sign-out", async (HttpContext http) =>
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Redirect(signInPage);
    })
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

// A saved view's export, handed to the browser as a file: a plain link the
// Saved views screen offers, so the download is the browser's own rather
// than bytes pushed through the circuit. The API tier is asked under the
// signed-in person's token, so it decides whose view this is and whether they
// may read it.
app.MapGet("/saved-views/{id:int}/export", async (int id, SavedViewsClient views, CancellationToken cancellationToken) =>
    await views.Export(id, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"saved-view-{id}.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// A saved view's counts as CSV, handed to the browser as a file the same way,
// from a plain link the Reports screen offers.
app.MapGet("/reports/saved-views/{id:int}/counts/export", async (
        int id, string groupBy, ReportsClient reports, CancellationToken cancellationToken) =>
    await reports.SavedViewCountsCsv(id, groupBy, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"saved-view-{id}-counts.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// A vocabulary table's CSV export, handed to the browser as a file the same
// way, from a plain link the Vocabulary screen offers.
app.MapGet("/vocabulary/{table}/export", async (string table, VocabularyClient vocabulary, CancellationToken cancellationToken) =>
    await vocabulary.Export(table, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"vocabulary-{table}.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// A PHIN value set or unit of measure table's CSV export, handed over the same way from the Value sets screen.
app.MapGet("/value-sets/{table}/export", async (string table, ValueSetsClient valueSets, CancellationToken cancellationToken) =>
    await valueSets.Export(table, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{table}.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// A notification guide's JSON, handed over the same way from its editor, and the
// header variable defaults as CSV from the Notification guides screen.
app.MapGet("/guides/{id:int}/export", async (int id, GuidesClient guides, CancellationToken cancellationToken) =>
    await guides.Export(id, cancellationToken) is var exported && exported.Value is { } guide
        ? Results.File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                guide, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }),
            "application/json",
            $"guide-{id}.json")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

app.MapGet("/guides/variable-defaults/export", async (GuidesClient guides, CancellationToken cancellationToken) =>
    await guides.ExportDefaults(cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "guide-variable-defaults.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// A code list's CSV export, handed over the same way from the Code lists screen.
app.MapGet("/codes/{list}/export", async (string list, CodesClient codes, CancellationToken cancellationToken) =>
    await codes.Export(list, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"codes-{list}.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// The Connecticut towns or the zip codes as CSV, handed over the same way from the Places screen.
app.MapGet("/places/{table}/export", async (string table, PlacesClient places, CancellationToken cancellationToken) =>
    await places.Export(table, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{table}.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// An agency's jurisdiction zip codes as CSV, handed over the same way from its edit screen.
app.MapGet("/agencies/{id:int}/zip-codes/export", async (int id, AgenciesClient agencies, CancellationToken cancellationToken) =>
    await agencies.ExportZipCodes(id, cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"agency-{id}-zip-codes.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// The facilities as CSV, handed over the same way from the Facilities screen.
app.MapGet("/facilities/export", async (FacilitiesClient facilities, CancellationToken cancellationToken) =>
    await facilities.Export(cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "facilities.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// The conditions as CSV, handed over the same way from the Conditions screen.
app.MapGet("/conditions/export", async (ConditionsClient conditions, CancellationToken cancellationToken) =>
    await conditions.Export(cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "conditions.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// The treatments as CSV, handed over the same way from the Treatments screen.
app.MapGet("/treatments/export", async (TreatmentsClient treatments, CancellationToken cancellationToken) =>
    await treatments.Export(cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "treatments.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

// The vaccine catalogue as CSV, handed over the same way from the Vaccines screen.
app.MapGet("/vaccines/export", async (VaccinesClient vaccines, CancellationToken cancellationToken) =>
    await vaccines.Export(cancellationToken) is var exported && exported.Value is { } csv
        ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "vaccines.csv")
        : Results.Problem(detail: exported.Failure, statusCode: StatusCodes.Status502BadGateway));

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
