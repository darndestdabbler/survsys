using System.Diagnostics;
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

builder.Services.AddConditions();
builder.Services.AddPersons();
builder.Services.AddPersonConditions();
builder.Services.AddClinical();
builder.Services.AddInvestigation();
builder.Services.AddAdministrative();
builder.Services.AddLabs();
builder.Services.AddExposures();
builder.Services.AddContactImport();
builder.Services.AddNotes();
builder.Services.AddTasks();
builder.Services.AddWorkQueue();

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
}

app.UseExceptionHandler();

// Not every failure arrives as an exception. A query-string value that will
// not parse into the parameter it is bound to never reaches an endpoint at
// all: the framework sets 400 and writes no body, which the exception handler
// above never sees. This turns those into Problem Details as well, so a
// caller reads one error shape from this tier and not two.
app.UseStatusCodePages();

// What the walkthrough harness waits on before its first click. This tier
// answers here only once startup has finished -- the migration gate above
// included -- so a 200 means it is serving a database at its own migration
// level, which is the whole of what the harness needs to know.
app.MapGet("/health/ready", () => Results.Ok("ready"));

app.MapConditions();
app.MapPersons();
app.MapAgencies();
app.MapPersonConditions();
app.MapClinical();
app.MapInvestigation();
app.MapAdministrative();
app.MapLabs();
app.MapExposures();
app.MapContactImport();
app.MapNotes();
app.MapTasks();
app.MapWorkQueue();

app.Run();

// What WebApplicationFactory needs of an entry point written as top-level
// statements: a Program type it can name. The integration suite names it.
public partial class Program;
