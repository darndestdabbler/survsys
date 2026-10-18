using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;

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
builder.Services.AddConditions();
builder.Services.AddAgencies();
builder.Services.AddFacilities();
builder.Services.AddCoreElements();
builder.Services.AddPersons();
builder.Services.AddPersonConditions();
builder.Services.AddClinical();
builder.Services.AddInvestigation();
builder.Services.AddAdministrative();
builder.Services.AddLabs();
builder.Services.AddExposures();
builder.Services.AddContactImport();
builder.Services.AddNotes();
builder.Services.AddAttachments();
builder.Services.AddTasks();
builder.Services.AddWorkQueue();
builder.Services.AddCaseSearch();
builder.Services.AddSavedViews();
builder.Services.AddMessageQueue();
builder.Services.AddNotificationQueue();
builder.Services.AddExportLog();
builder.Services.AddOutbreaks();
builder.Services.AddForms();
builder.Services.AddTreatments();
builder.Services.AddVaccines();
builder.Services.AddIbis();
builder.Services.AddReports();

// The inbound ELR intake, a background worker that polls a file drop and
// queues each message for review. Off unless a drop directory is configured,
// so it is inert for a deployment that has not turned it on.
builder.Services.Configure<ElrIntakeOptions>(
    builder.Configuration.GetSection(ElrIntakeOptions.SectionName));
builder.Services.AddHostedService<ElrIntakeService>();

// Direct transmission of a generated notification to CDC, in place of the
// legacy Mirth export queue. A typed HttpClient so the transport is injected
// and testable; off unless an endpoint is configured, so it is inert for a
// deployment that has not turned it on.
builder.Services.Configure<CdcTransmissionOptions>(
    builder.Configuration.GetSection(CdcTransmissionOptions.SectionName));
builder.Services.AddHttpClient<CdcTransmitter>();

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

// What the walkthrough harness waits on before its first click. This tier
// answers here only once startup has finished -- the migration gate above
// included -- so a 200 means it is serving a database at its own migration
// level, which is the whole of what the harness needs to know.
app.MapGet("/health/ready", () => Results.Ok("ready")).AllowAnonymous();

app.MapAccounts();
app.MapUsers();
app.MapRoles();
app.MapAccessAudit();
app.MapConditions();
app.MapPersons();
app.MapAgencies();
app.MapFacilities();
app.MapCoreElements();
app.MapPersonConditions();
app.MapClinical();
app.MapInvestigation();
app.MapAdministrative();
app.MapLabs();
app.MapExposures();
app.MapContactImport();
app.MapNotes();
app.MapAttachments();
app.MapTasks();
app.MapWorkQueue();
app.MapCaseSearch();
app.MapSavedViews();
app.MapMessageQueue();
app.MapNotificationQueue();
app.MapExportLog();
app.MapOutbreaks();
app.MapForms();
app.MapTreatments();
app.MapVaccines();
app.MapIbis();
app.MapReports();

app.Run();

// What WebApplicationFactory needs of an entry point written as top-level
// statements: a Program type it can name. The integration suite names it.
public partial class Program;
