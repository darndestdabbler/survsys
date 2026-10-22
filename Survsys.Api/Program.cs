using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;

// The API tier: the only module that reaches the database, and so the only
// one that starts with a connection string.
const string connectionStringName = "Survsys";

// Off by default, and the default is the production behaviour: a database
// below this assembly's migration level is refused rather than altered. On,
// this tier brings the database up to its own migration level at startup and
// serves -- which is how the walkthrough harness gets a schema at all, since
// this is the only tier permitted to reach the database. A deployment never
// sets it: the handoff carries the idempotent script for that.
const string applyMigrationsKey = "Survsys:ApplyMigrations";

// The administrator ensured at startup, so a fresh database has someone who
// can sign in and create everyone else.
const string administratorUserNameKey = "Survsys:Administrator:UserName";
const string administratorPasswordKey = "Survsys:Administrator:Password";

// The folder this tier's data protection keys are kept in, where it is set.
const string dataProtectionKeysPathKey = "Survsys:DataProtectionKeysPath";

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString(connectionStringName);

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        $"No connection string named '{connectionStringName}' is configured. The API tier "
        + "is the only tier that reaches the database and cannot start without one: set "
        + $"ConnectionStrings:{connectionStringName} for this environment.");
}

builder.Services.AddDbContext<SurvsysDbContext>(options => options.UseSqlServer(connectionString));

// The keys that protect bearer tokens, in the folder configuration names, so
// a token outlives an app-pool recycle and is read by every server of this
// tier. The application name is fixed because the default is the content
// root, which differs from one server to the next. Unset, the keys are the
// framework's default and a recycle signs everyone out.
if (builder.Configuration[dataProtectionKeysPathKey] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("Survsys.Api");
}

// Every failure leaves as Problem Details (RFC 7807), carrying a correlation
// id the caller can quote back and nothing else: no exception text, no SQL,
// no server path and no connection detail. What went wrong is in this tier's
// log under the same id, where it is readable by someone entitled to read it.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] =
        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

// Every request but signing in and the readiness probe is authorised, by one
// fallback policy rather than by a line beside each endpoint, so an endpoint
// mapped later is covered the day it is mapped. The caller must carry a
// bearer token from /api/sign-in, and PrivilegeRules names the privilege the
// request needs. A caller who is not signed in is answered 401 and one who
// lacks the privilege 403, both as Problem Details by UseStatusCodePages below.
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(options => options.BearerTokenExpiration = TimeSpan.FromHours(8));
builder.Services.AddAuthorization(options => options.FallbackPolicy =
    new AuthorizationPolicyBuilder(BearerTokenDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireAssertion(context => context.Resource is HttpContext http && PrivilegeRules.Allows(
            context.User.FindAll(PrivilegeRules.PrivilegeClaim).Select(claim => claim.Value),
            http.Request.Method,
            http.Request.Path))
        .Build());

builder.Services.AddAccounts();
builder.Services.AddUsers();
builder.Services.AddRoles();
builder.Services.AddAccessAudit();
builder.Services.AddAudit();
builder.Services.AddConditions();
builder.Services.AddAgencies();
builder.Services.AddFacilities();
builder.Services.AddCoreElements();
builder.Services.AddPersons();
builder.Services.AddPersonMatches(builder.Configuration);
builder.Services.AddPersonMerges();
builder.Services.AddPersonConditions();
builder.Services.AddEditLocks();
builder.Services.AddClinical();
builder.Services.AddInvestigation();
builder.Services.AddAdministrative();
builder.Services.AddLabs();
builder.Services.AddExposures();
builder.Services.AddContactImport();
builder.Services.AddContactEvents();
builder.Services.AddNotes();
builder.Services.AddAttachments();
builder.Services.AddTasks();
builder.Services.AddWorkQueue();
builder.Services.AddCaseSearch();
builder.Services.AddSavedViews();
builder.Services.AddMessageQueue();
builder.Services.AddMessageStatistics();
builder.Services.AddNotificationQueue();
builder.Services.AddExportLog();
builder.Services.AddReconciliationCutoff();
builder.Services.AddOutbreaks();
builder.Services.AddForms();
builder.Services.AddTreatments();
builder.Services.AddVaccines();
builder.Services.AddEncounters();
builder.Services.AddCodes();
builder.Services.AddVocabulary();
builder.Services.AddPhinVariables();
builder.Services.AddRuleSets();
builder.Services.AddGuides();
builder.Services.AddHepLabGroups();
builder.Services.AddPlaces();
builder.Services.AddIbis();
builder.Services.AddReports();
builder.Services.AddSystemSettings();

// The inbound ELR intake: a background worker that polls a file drop, and an
// MLLP listener beside it, each queuing every message for review. Each is off
// unless configured (a drop directory, an MLLP port), so they are inert for a
// deployment that has not turned them on.
builder.Services.Configure<ElrIntakeOptions>(
    builder.Configuration.GetSection(ElrIntakeOptions.SectionName));
// The conformance profile each sender is checked against at intake; a sender
// with none is not checked.
builder.Services.Configure<ConformanceOptions>(
    builder.Configuration.GetSection(ConformanceOptions.SectionName));
// Which of an address's county and county equivalent each program reports.
builder.Services.Configure<CountyPrograms>(
    builder.Configuration.GetSection(CountyPrograms.SectionName));
builder.Services.AddSingleton<IMessageParser, OruR01Parser>();
builder.Services.AddHostedService<ElrIntakeService>();
builder.Services.AddHostedService<MllpListener>();

// Automatic assignment and the graylist: a background worker that assigns the
// queued messages it can assign with confidence, and releases and prunes
// graylisted ones. Off unless a poll interval is configured.
builder.Services.Configure<MessageProcessingOptions>(
    builder.Configuration.GetSection(MessageProcessingOptions.SectionName));
builder.Services.AddHostedService<MessageProcessingService>();

// The batch notification e-mail of assigned messages to each agency's
// notification addresses, over SMTP. Off unless an interval, host and sender
// are configured.
builder.Services.Configure<MessageNotificationOptions>(
    builder.Configuration.GetSection(MessageNotificationOptions.SectionName));
builder.Services.AddHostedService<MessageNotificationService>();

// Direct transmission of a generated notification to CDC, in place of the
// legacy Mirth export queue. A typed HttpClient so the transport is injected
// and testable; off unless an endpoint is configured, so it is inert for a
// deployment that has not turned it on.
builder.Services.Configure<CdcTransmissionOptions>(
    builder.Configuration.GetSection(CdcTransmissionOptions.SectionName));
builder.Services.AddHttpClient<CdcTransmitter>();
// A case's notification message, built from its guide's PHIN variables and
// their rule sets.
builder.Services.AddScoped<NotificationMessageBuilder>();

// The export queue worker: builds, sends and logs each released case's
// notification. Off unless a poll interval is configured.
builder.Services.Configure<NotificationQueueOptions>(
    builder.Configuration.GetSection(NotificationQueueOptions.SectionName));
builder.Services.AddHostedService<NotificationQueueService>();

// Transmission of a case to IBIS, the external epidemiology reporting system:
// the same shape as the CDC transmitter, and off the same way until an
// endpoint is configured.
builder.Services.Configure<IbisTransmissionOptions>(
    builder.Configuration.GetSection(IbisTransmissionOptions.SectionName));
builder.Services.AddHttpClient<IbisTransmitter>();

var app = builder.Build();

// Before the first request, not during it: a database behind this assembly's
// migrations is one loud startup failure rather than a 500 on whichever
// request happens to touch a column that is not there yet.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>().Database;

    if (builder.Configuration.GetValue<bool>(applyMigrationsKey))
    {
        await database.MigrateAsync();
    }
    else
    {
        MigrationGate.AssertNonePending(await database.GetPendingMigrationsAsync());
    }

    // A first administrator, from configuration and never from a file in the
    // repository: set Survsys__Administrator__UserName and
    // Survsys__Administrator__Password where the tier runs. Without both,
    // nothing is created, and a database with no user has nobody to sign in.
    await scope.ServiceProvider.GetRequiredService<AccountQueries>().EnsureAdministrator(
        builder.Configuration[administratorUserNameKey],
        builder.Configuration[administratorPasswordKey],
        CancellationToken.None);
}

app.UseExceptionHandler();

// Not every failure arrives as an exception. A query-string value that will
// not parse into the parameter it is bound to never reaches an endpoint at
// all: the framework sets 400 and writes no body, which the exception handler
// above never sees. This turns those into Problem Details as well, so a
// caller reads one error shape from this tier and not two.
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

// A case the caller's agencies neither own nor have been shared is answered
// 404 on every route about it; CaseAccess says which routes those are.
app.UseCaseAccess();

// A route that classified a case answers with what was decided.
app.UseCaseClassificationHeader();

// What the walkthrough harness waits on before its first click. This tier
// answers here only once startup has finished -- the migration gate above
// included -- so a 200 means it is serving a database at its own migration
// level, which is the whole of what the harness needs to know.
app.MapGet("/health/ready", () => Results.Ok("ready")).AllowAnonymous();

app.MapAccounts();
app.MapUsers();
app.MapRoles();
app.MapAccessAudit();
app.MapAudit();
app.MapConditions();
app.MapVocabulary();
app.MapValueSets();
app.MapPhinVariables();
app.MapRuleSets();
app.MapGuides();
app.MapHepLabGroups();
app.MapPersons();
app.MapPersonMatches();
app.MapPersonMerges();
app.MapAgencies();
app.MapFacilities();
app.MapCoreElements();
app.MapPersonConditions();
app.MapEditLocks();
app.MapClinical();
app.MapInvestigation();
app.MapAdministrative();
app.MapLabs();
app.MapExposures();
app.MapContactImport();
app.MapContactEvents();
app.MapNotes();
app.MapAttachments();
app.MapTasks();
app.MapWorkQueue();
app.MapCaseSearch();
app.MapSavedViews();
app.MapMessageQueue();
app.MapMessageStatistics();
app.MapNotificationQueue();
app.MapExportLog();
app.MapReconciliationCutoff();
app.MapOutbreaks();
app.MapForms();
app.MapTreatments();
app.MapVaccines();
app.MapEncounters();
app.MapCodes();
app.MapPlaces();
app.MapIbis();
app.MapReports();
app.MapSystemSettings();

app.Run();

// What WebApplicationFactory needs of an entry point written as top-level
// statements: a Program type it can name. The integration suite names it.
public partial class Program;
